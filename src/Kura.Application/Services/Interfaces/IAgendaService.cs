namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Agenda;

public interface IAgendaService
{
    Task<AgendaResponseDto> GetAgendaAsync(DateTime dataInicio, DateTime dataFim, long? idVeterinario);
    Task<AgendamentoItemDto> AtualizarStatusAsync(long id, AtualizarStatusAgendamentoDto dto);

    /// <summary>REC-10 — <c>POST /api/v1/agendamentos</c>.</summary>
    Task<AgendamentoItemDto> CriarAsync(AgendamentoCreateDto dto);

    /// <summary>REC-11 — <c>POST /api/v1/agendamentos/{id}/checkin</c>. Idempotente: 2ª chamada
    /// devolve o estado atual sem sobrescrever o horário nem incrementar <c>NrVersion</c>.</summary>
    Task<AgendamentoItemDto> CheckinAsync(long id, RegistrarEventoRecepcaoDto dto);

    /// <summary>REC-11 — <c>POST /api/v1/agendamentos/{id}/inicio-atendimento</c>. Permitido sem
    /// check-in prévio (walk-in) — nunca preenche <c>DtCheckin</c> como efeito colateral.
    /// Idempotente, mesmo contrato de <see cref="CheckinAsync"/>.</summary>
    Task<AgendamentoItemDto> IniciarAtendimentoAsync(long id, RegistrarEventoRecepcaoDto dto);
}
