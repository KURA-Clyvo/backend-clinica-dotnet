namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.TipoEvento;

public interface ITipoEventoService
{
    Task<IEnumerable<TipoEventoResponseDto>> GetAllAsync();
    Task<TipoEventoResponseDto> GetByIdAsync(long id);
}
