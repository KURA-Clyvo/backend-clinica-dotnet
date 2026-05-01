namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Pet;

public interface IPetService
{
    Task<IEnumerable<PetResponseDto>> GetAllAsync();
    Task<PetResponseDto> GetByIdAsync(long id);
    Task<PetResponseDto> CreateAsync(PetCreateDto dto);
    Task<PetResponseDto> UpdateAsync(long id, PetUpdateDto dto);
    Task SoftDeleteAsync(long id);
}
