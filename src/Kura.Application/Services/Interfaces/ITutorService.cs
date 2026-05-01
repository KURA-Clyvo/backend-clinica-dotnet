namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Tutor;

public interface ITutorService
{
    Task<IEnumerable<TutorResponseDto>> GetAllAsync();
    Task<TutorResponseDto> GetByIdAsync(long id);
    Task<TutorResponseDto> CreateAsync(TutorCreateDto dto);
    Task<TutorResponseDto> UpdateAsync(long id, TutorUpdateDto dto);
    Task SoftDeleteAsync(long id);
}
