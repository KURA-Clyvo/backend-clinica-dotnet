namespace Kura.Application.Services;

using Kura.Application.DTOs.Teleconsulta;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class TeleconsultaService : ITeleconsultaService
{
    private const string TipoConsentimentoTeleorientacao = "TELEORIENTACAO";
    private const string ProvedorDaily = "DAILY";

    private readonly IAgendamentoRepository _agendamentoRepository;
    private readonly IConsentimentoRepository _consentimentoRepository;
    private readonly IDailyService _dailyService;
    private readonly IClinicaContext _clinicaContext;
    private readonly IUnitOfWork _uow;
    private readonly IRelogioClinica _relogioClinica;

    public TeleconsultaService(
        IAgendamentoRepository agendamentoRepository,
        IConsentimentoRepository consentimentoRepository,
        IDailyService dailyService,
        IClinicaContext clinicaContext,
        IUnitOfWork uow,
        IRelogioClinica relogioClinica)
    {
        _agendamentoRepository = agendamentoRepository;
        _consentimentoRepository = consentimentoRepository;
        _dailyService = dailyService;
        _clinicaContext = clinicaContext;
        _uow = uow;
        _relogioClinica = relogioClinica;
    }

    public async Task<TeleconsultaResponseDto> CriarOuObterSalaAsync(long idAgendamento)
    {
        var agendamento = await GetAgendamentoAsync(idAgendamento);
        await GarantirConsentimentoAsync(agendamento);

        if (agendamento.StTeleconsulta && !string.IsNullOrWhiteSpace(agendamento.DsSalaUrl))
            return ToDto(agendamento, fallbackManual: false);

        var nomeSala = $"kura-agendamento-{agendamento.Id}";
        var resultado = await _dailyService.CriarSalaAsync(nomeSala);

        if (!resultado.Sucesso)
            return ToDto(agendamento, fallbackManual: true);

        agendamento.DsSalaUrl = resultado.Url;
        agendamento.DsProvedorVideo = ProvedorDaily;
        agendamento.StTeleconsulta = true;
        // REC-08/A-5 -- DT_INICIO_SESSAO é coluna de AGENDAMENTO (V10); grava hora local de SP,
        // junto do chamador de ContarTeleorientacoesHojeAsync (DashboardService), que agora
        // compara com o mesmo relógio (call sites #5/#6 do G0 item 3).
        agendamento.DtInicioSessao = _relogioClinica.Agora();

        _agendamentoRepository.Update(agendamento);
        await _uow.CommitAsync();

        return ToDto(agendamento, fallbackManual: false);
    }

    public async Task<TeleconsultaResponseDto> ObterSalaAsync(long idAgendamento)
    {
        var agendamento = await GetAgendamentoAsync(idAgendamento);
        return ToDto(agendamento, fallbackManual: false);
    }

    private async Task<Agendamento> GetAgendamentoAsync(long idAgendamento)
    {
        return await _agendamentoRepository.GetByIdAsync(idAgendamento, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("Agendamento", idAgendamento);
    }

    private async Task GarantirConsentimentoAsync(Agendamento agendamento)
    {
        var idTutor = agendamento.IdTutor;
        var consentimento = idTutor is null
            ? null
            : await _consentimentoRepository.GetMaisRecenteAsync(idTutor.Value, TipoConsentimentoTeleorientacao);

        if (consentimento is null || consentimento.StAceito != 'S')
            throw new RegraDeNegocioException(
                "Consentimento de teleorientação não registrado (ou não aceito) para o tutor deste agendamento.");
    }

    private static TeleconsultaResponseDto ToDto(Agendamento agendamento, bool fallbackManual) => new()
    {
        IdAgendamento = agendamento.Id,
        DsSalaUrl = agendamento.DsSalaUrl,
        DsProvedorVideo = agendamento.DsProvedorVideo,
        DtInicioSessao = agendamento.DtInicioSessao,
        StFallbackManual = fallbackManual
    };
}
