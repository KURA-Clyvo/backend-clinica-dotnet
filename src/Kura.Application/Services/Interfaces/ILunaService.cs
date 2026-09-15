namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Common;
using Kura.Application.DTOs.Luna;

public interface ILunaService
{
    Task<RelatorioTriagensDto> GerarRelatorioAsync(DateTime dataInicio, DateTime dataFim);

    /// <summary>
    /// TASK-67: POST /api/v1/luna/interactions. Deriva ID_CLINICA do tutor (ID_CLINICA
    /// é NOT NULL e a Luna nunca envia esse campo).
    /// </summary>
    Task<InteractionResponseDto> RegistrarInteracaoAsync(InteractionRequestDto dto);

    /// <summary>
    /// TASK-67: POST /api/v1/luna/triage.
    /// </summary>
    Task<TriageResponseDto> RegistrarTriagemAsync(TriageRequestDto dto);

    /// <summary>
    /// LU-08: GET /api/v1/luna/triagens — JWT de clínica (idClinica sai do token via
    /// IClinicaContext, nunca de parâmetro do cliente). Período opcional; quando os
    /// dois extremos são informados, o intervalo respeita o mesmo teto de
    /// GerarRelatorioAsync (90 dias).
    /// </summary>
    Task<PagedResultDto<TriagemListaItemDto>> ListarTriagensAsync(
        string? urgencia,
        DateTime? dataInicio,
        DateTime? dataFim,
        int page,
        int pageSize);
}
