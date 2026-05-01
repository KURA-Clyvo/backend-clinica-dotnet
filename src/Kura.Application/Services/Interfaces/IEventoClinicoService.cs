namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.EventoClinico;

public interface IEventoClinicoService
{
    Task<EventoClinicoResponseDto> GetByIdAsync(long id);
    Task<IEnumerable<EventoClinicoResponseDto>> GetByFiltersAsync(
        long? petId, string? tipo, DateTime? dataInicio, DateTime? dataFim, long? veterinarioId);
    Task<IEnumerable<TimelineItemDto>> GetTimelineAsync(long idPet);
}
