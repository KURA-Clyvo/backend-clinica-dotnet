namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Veterinario;

public interface IVeterinarioService
{
    Task<IEnumerable<VeterinarioResponseDto>> GetAllAsync();
    Task<VeterinarioResponseDto> GetByIdAsync(long id);
    Task<IEnumerable<VeterinarioResponseDto>> GetByClinicaAsync(long idClinica);
    Task<VeterinarioResponseDto> CreateAsync(VeterinarioCreateDto dto);
    Task<VeterinarioResponseDto> UpdateAsync(long id, VeterinarioUpdateDto dto);
    Task SoftDeleteAsync(long id);
}
