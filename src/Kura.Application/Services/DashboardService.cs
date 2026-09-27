namespace Kura.Application.Services;

using Kura.Application.DTOs.Dashboard;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

public sealed class DashboardService : IDashboardService
{
    private readonly IEventoClinicoRepository _eventoRepository;
    private readonly IRepository<AlertaTemperatura> _alertaRepository;
    private readonly IRepository<Pet> _petRepository;
    private readonly IRepository<Vacina> _vacinaRepository;
    private readonly IAgendamentoRepository _agendamentoRepository;
    private readonly IClinicaContext _clinicaContext;
    private readonly IRelogioClinica _relogioClinica;

    public DashboardService(
        IEventoClinicoRepository eventoRepository,
        IRepository<AlertaTemperatura> alertaRepository,
        IRepository<Pet> petRepository,
        IRepository<Vacina> vacinaRepository,
        IAgendamentoRepository agendamentoRepository,
        IClinicaContext clinicaContext,
        IRelogioClinica relogioClinica)
    {
        _eventoRepository = eventoRepository;
        _alertaRepository = alertaRepository;
        _petRepository = petRepository;
        _vacinaRepository = vacinaRepository;
        _agendamentoRepository = agendamentoRepository;
        _clinicaContext = clinicaContext;
        _relogioClinica = relogioClinica;
    }

    public async Task<DashboardHojeDto> GetHojeAsync()
    {
        // REC-08 -- "hoje" (UTC) segue servindo SÓ o que compara com EventoClinico.DtEvento
        // abaixo (fora do escopo da REC-08 -- mesmo defeito de fuso, tabela diferente, ver
        // G0 item 3). Tudo que compara com AGENDAMENTO usa o relógio local de SP.
        var hoje = DateTime.UtcNow.Date;
        var agora = _relogioClinica.Agora();
        var idClinica = _clinicaContext.IdClinica;

        // EventoClinico ESTÁ no ApplyTenantFilters (KuraDbContext), então GetByFiltersAsync já
        // vem escopado pela clínica do contexto -- não precisa (e não deve) repetir o filtro aqui.
        var todosEventos = await _eventoRepository.GetByFiltersAsync(null, null, null, null, null);
        var consultasHoje = todosEventos.Count(e => e.DtEvento.Date == hoje);
        var retornosPendentes = todosEventos.Count(e => e.DtEvento.Date > hoje);

        // FD-17 item 2 -- pets distintos com evento HOJE, sem teto. Mesmo critério de "hoje" já
        // usado acima para consultasHoje (e.DtEvento.Date == hoje) -- não inventar segunda
        // convenção de data neste método.
        var pacientesAtendidosHoje = todosEventos
            .Where(e => e.DtEvento.Date == hoje)
            .Select(e => e.IdPet)
            .Distinct()
            .Count();

        var alertas = await _alertaRepository.GetAllAsync();
        var alertasAtivos = alertas.Count(a => !a.StResolvido);

        var ultimosPets = todosEventos
            .OrderByDescending(e => e.DtEvento)
            .DistinctBy(e => e.IdPet)
            .Take(5)
            .Select(e => new PetResumoDto
            {
                Id = e.IdPet,
                NmPet = e.Pet?.NmPet ?? string.Empty,
                UltimoAtendimento = e.DtEvento
            })
            .ToList();

        // FD-17 item 1 -- Agendamento NÃO tem HasQueryFilter (é a única exceção do
        // ApplyTenantFilters), então idClinica precisa ser passado explicitamente aqui, senão
        // o dashboard mistura agendamento de todas as clínicas.
        var proximosAgendamentos = (await _agendamentoRepository.GetProximosDoDiaAsync(idClinica, agora, 3))
            .Select(MapParaResumo)
            .ToList();

        // FD-17 item 3 -- também escopado por clínica pela mesma razão acima. REC-08: "hoje"
        // aqui é agora.Date (local de SP), consistente com quem grava DtInicioSessao
        // (TeleconsultaService, também via IRelogioClinica) -- não o "hoje" UTC de EventoClinico.
        var teleorientacoesHoje = await _agendamentoRepository.ContarTeleorientacoesHojeAsync(idClinica, agora.Date);

        return new DashboardHojeDto
        {
            TotalConsultasHoje = consultasHoje,
            TotalAlertasAtivos = alertasAtivos,
            TotalRetornosPendentes = retornosPendentes,
            TotalPacientesAtendidosHoje = pacientesAtendidosHoje,
            TotalTeleorientacoesHoje = teleorientacoesHoje,
            UltimosPetsAtendidos = ultimosPets,
            ProximosAgendamentos = proximosAgendamentos
        };
    }

    public async Task<IEnumerable<object>> GetAlertasAsync()
    {
        var alertasTemp = await _alertaRepository.FindAsync(a => !a.StResolvido);

        var hoje = DateTime.UtcNow.Date;
        var limite30Dias = hoje.AddDays(30);
        var vacinas = await _vacinaRepository.FindAsync(
            v => v.DtProximaDose.HasValue &&
                 v.DtProximaDose.Value.Date >= hoje &&
                 v.DtProximaDose.Value.Date <= limite30Dias);

        var resultado = new List<object>();
        resultado.AddRange(alertasTemp.Select(a => (object)new
        {
            Tipo = "TEMPERATURA",
            a.Id,
            a.DsTipoAlerta,
            a.DsMensagem,
            a.DtCriacao
        }));
        resultado.AddRange(vacinas.Select(v => (object)new
        {
            Tipo = "VACINA_VENCENDO",
            v.Id,
            DsTipoAlerta = "PROXIMA_DOSE",
            DsMensagem = $"Vacina '{v.NmVacina}' com próxima dose em {v.DtProximaDose:dd/MM/yyyy}.",
            v.DtCriacao
        }));

        return resultado;
    }

    private const int LimiteAgendamentosRecentes = 10;

    public async Task<IEnumerable<AgendamentoResumoDto>> GetRecentesAsync()
    {
        // FD-17 item 1 -- mesma correção de idClinica explícito (ver GetHojeAsync). REC-08:
        // referência local de SP, não UtcNow -- ver IRelogioClinica.
        var recentes = await _agendamentoRepository.GetRecentesAsync(
            _clinicaContext.IdClinica, _relogioClinica.Agora(), LimiteAgendamentosRecentes);
        return recentes.Select(MapParaResumo).ToList();
    }

    private static AgendamentoResumoDto MapParaResumo(Agendamento a) => new()
    {
        Id = a.Id,
        NmPaciente = a.NmPaciente ?? string.Empty,
        DtAgendamento = a.DtAgendamento,
        DsServico = a.DsServico ?? string.Empty,
        StStatus = a.StStatus ?? string.Empty
    };
}
