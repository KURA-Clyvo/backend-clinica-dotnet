namespace Kura.Application.Services.Interfaces;

using Kura.Domain.Paginacao;
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

    /// <summary>
    /// S4-NET-04 — <c>GET /api/v2/agendamentos</c>. Mesma regra de intervalo da v1 (fim ≥ início,
    /// máx. 31 dias ⇒ <c>RegraDeNegocioException</c> ⇒ 422). Escopo de clínica vem do
    /// <c>IClinicaContext</c>, nunca do cliente.
    /// </summary>
    Task<PaginaDe<AgendamentoItemDto>> ListarPaginadoAsync(ConsultaAgendamentos consulta);
}
