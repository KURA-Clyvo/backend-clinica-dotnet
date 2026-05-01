namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.AlertaTemperatura;

public interface IAlertaTemperaturaService
{
    Task<IEnumerable<AlertaTemperaturaResponseDto>> GetByDispositivoAsync(long idDispositivo);
    Task ResolverAsync(long id);
}
