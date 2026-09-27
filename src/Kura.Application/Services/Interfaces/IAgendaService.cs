namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Agenda;

public interface IAgendaService
{
    Task<AgendaResponseDto> GetAgendaAsync(DateTime dataInicio, DateTime dataFim, long? idVeterinario);
    Task<AgendamentoItemDto> AtualizarStatusAsync(long id, AtualizarStatusAgendamentoDto dto);

    /// <summary>REC-10 — <c>POST /api/v1/agendamentos</c>.</summary>
    Task<AgendamentoItemDto> CriarAsync(AgendamentoCreateDto dto);
}
