namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.LeituraTemperatura;

public interface ILeituraTemperaturaService
{
    Task<LeituraTemperaturaResponseDto> IngerirAsync(LeituraTemperaturaCreateDto dto);
    Task<IEnumerable<LeituraTemperaturaResponseDto>> GetByDispositivoAsync(long idDispositivo);
}
