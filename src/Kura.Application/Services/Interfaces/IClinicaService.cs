namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Clinica;

public interface IClinicaService
{
    Task<IEnumerable<ClinicaResponseDto>> GetAllAsync();
    Task<ClinicaResponseDto> GetByIdAsync(long id);
    Task<ClinicaResponseDto> CreateAsync(ClinicaCreateDto dto);
    Task<ClinicaResponseDto> UpdateAsync(long id, ClinicaUpdateDto dto);
    Task SoftDeleteAsync(long id);
}
