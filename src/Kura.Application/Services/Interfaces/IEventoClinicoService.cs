namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.EventoClinico;

public interface IEventoClinicoService
{
    Task<EventoClinicoResponseDto> GetByIdAsync(long id);
    Task<IEnumerable<EventoClinicoResponseDto>> GetByPetAsync(long idPet);
}
