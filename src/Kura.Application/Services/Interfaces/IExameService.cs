namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Exame;

public interface IExameService
{
    Task<ExameResponseDto> CreateAsync(ExameCreateDto dto);
    Task<ExameResponseDto> GetByEventoClinicoAsync(long idEvento);
    Task<IEnumerable<ExameResponseDto>> GetByPetAsync(long idPet);
}
