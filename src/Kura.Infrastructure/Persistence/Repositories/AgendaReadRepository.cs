namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.CrossCutting.Observability;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Domain.Paginacao;
using Microsoft.EntityFrameworkCore;

public class AgendaReadRepository(KuraDbContext context) : IAgendamentoReadRepository
{
    public async Task<IEnumerable<Agendamento>> GetByIntervaloAsync(
        long idClinica, DateTime dataInicio, DateTime dataFim, long? idVeterinario)
    {
        // S3D-04b: span-filho de camada Infrastructure — filho do span de
        // Application acima (AgendaService), que por sua vez é filho do span HTTP.
        // Prova a hierarquia de 3 níveis: API -> Application -> Infrastructure -> Oracle.
        using var activity = KuraActivitySource.Instancia.StartActivity("Infrastructure.AgendaReadRepository.GetByIntervaloAsync");
        activity?.SetTag("kura.layer", "Infrastructure");
        activity?.SetTag("kura.id_clinica", idClinica);

        // REC-08/G0 item 3 -- achado novo: dataInicio/dataFim chegam como MEIA-NOITE (o app
        // manda "YYYY-MM-DD" sem hora), e o intervalo era FECHADO nos dois extremos
        // (`<= dataFim`). Um agendamento marcado às 10h do último dia do intervalo (inclusive
        // "dataInicio == dataFim", a consulta de um dia só) ficava de fora, porque 10:00 > 00:00.
        // Fim EXCLUSIVO (`< dataFim.Date + 1 dia`) resolve sem depender de hora: cobre o dia
        // inteiro de dataFim. `.Date` nos dois extremos normaliza caso algum dia um chamador
        // comece a mandar hora (contrato do controller continua "datas inclusive").
        var query = context.Agendamentos
            .Include(a => a.Pet)
            .Include(a => a.Tutor)
            .Include(a => a.Veterinario)
            // REC-09/A-7: ID_TRIAGEM_ORIGEM é FK opcional -> LEFT JOIN. O HasQueryFilter
            // de TriagemLuna (KuraDbContext.ApplyTenantFilters) continua ativo sobre a
            // navegação: triagem de outra clínica não passa no filtro e a propriedade
            // fica null (não derruba a linha de Agendamento) — provado em
            // AgendaReadRepositoryTests com duas clínicas.
            .Include(a => a.TriagemOrigem)
            .Where(a => a.IdClinica == idClinica
                     && a.DtAgendamento >= dataInicio.Date
                     && a.DtAgendamento < dataFim.Date.AddDays(1));

        if (idVeterinario.HasValue)
            query = query.Where(a => a.IdVeterinario == idVeterinario.Value);

        return await query.OrderBy(a => a.DtAgendamento).ToListAsync();
    }

    public async Task<PaginaDe<Agendamento>> ListarPaginadoAsync(long idClinica, ConsultaAgendamentos consulta)
    {
        using var activity = KuraActivitySource.Instancia.StartActivity("Infrastructure.AgendaReadRepository.ListarPaginadoAsync");
        activity?.SetTag("kura.layer", "Infrastructure");
        activity?.SetTag("kura.id_clinica", idClinica);

        // Agendamento NÃO tem query filter: o IdClinica abaixo é a ÚNICA barreira de tenant.
        // Mesma regra de intervalo do GetByIntervaloAsync (fim exclusivo no dia seguinte).
        var inicio = consulta.DataInicio.Date;
        var fimExclusivo = consulta.DataFim.Date.AddDays(1);
        var filtrado = context.Agendamentos
            .Where(a => a.IdClinica == idClinica
                     && a.DtAgendamento >= inicio
                     && a.DtAgendamento < fimExclusivo);

        if (consulta.IdVeterinario.HasValue)
            filtrado = filtrado.Where(a => a.IdVeterinario == consulta.IdVeterinario.Value);
        if (!string.IsNullOrWhiteSpace(consulta.Status))
        {
            var status = consulta.Status;
            filtrado = filtrado.Where(a => a.StStatus == status);
        }

        var total = await filtrado.LongCountAsync();

        var ordenado = consulta.Ordem switch
        {
            OrdenacaoAgendamento.DtAgendamentoDesc => filtrado.OrderByDescending(a => a.DtAgendamento).ThenBy(a => a.Id),
            OrdenacaoAgendamento.IdAsc => filtrado.OrderBy(a => a.Id),
            OrdenacaoAgendamento.IdDesc => filtrado.OrderByDescending(a => a.Id),
            _ => filtrado.OrderBy(a => a.DtAgendamento).ThenBy(a => a.Id),
        };

        var itens = await ordenado
            .Skip((consulta.Pagina - 1) * consulta.TamanhoPagina)
            .Take(consulta.TamanhoPagina)
            .Include(a => a.Pet)
            .Include(a => a.Tutor)
            .Include(a => a.Veterinario)
            .Include(a => a.TriagemOrigem)
            .ToListAsync();
        return new PaginaDe<Agendamento>(itens, consulta.Pagina, consulta.TamanhoPagina, total);
    }
}
