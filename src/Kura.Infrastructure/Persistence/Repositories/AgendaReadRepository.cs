namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.CrossCutting.Observability;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
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
            .Where(a => a.IdClinica == idClinica
                     && a.DtAgendamento >= dataInicio.Date
                     && a.DtAgendamento < dataFim.Date.AddDays(1));

        if (idVeterinario.HasValue)
            query = query.Where(a => a.IdVeterinario == idVeterinario.Value);

        return await query.OrderBy(a => a.DtAgendamento).ToListAsync();
    }
}
