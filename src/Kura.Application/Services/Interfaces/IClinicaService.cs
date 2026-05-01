namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Clinica;

public interface IClinicaService
{
    Task<ClinicaResponseDto> GetByIdAsync(long id);
    Task<ClinicaResponseDto> UpdateAsync(long id, ClinicaUpdateDto dto);
}
