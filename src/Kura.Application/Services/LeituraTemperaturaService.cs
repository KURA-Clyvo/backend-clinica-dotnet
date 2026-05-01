namespace Kura.Application.Services;

using Kura.Application.DTOs.LeituraTemperatura;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

public sealed class LeituraTemperaturaService : ILeituraTemperaturaService
{
    private readonly IRepository<LeituraTemperatura> _leituraRepository;
    private readonly IRepository<AlertaTemperatura> _alertaRepository;
    private readonly IUnitOfWork _uow;
    private readonly decimal _tempMax;
    private readonly decimal _tempMin;

    public LeituraTemperaturaService(
        IRepository<LeituraTemperatura> leituraRepository,
        IRepository<AlertaTemperatura> alertaRepository,
        IUnitOfWork uow,
        IConfiguration configuration)
    {
        _leituraRepository = leituraRepository;
        _alertaRepository = alertaRepository;
        _uow = uow;
        _tempMax = configuration.GetValue<decimal>("IoT:TempMaxCelsius", 39.0m);
        _tempMin = configuration.GetValue<decimal>("IoT:TempMinCelsius", 10.0m);
    }

    public async Task<LeituraTemperaturaResponseDto> IngerirAsync(LeituraTemperaturaCreateDto dto)
    {
        var leitura = new LeituraTemperatura
        {
            IdDispositivoIot = dto.IdDispositivoIot,
            VlTemperatura = dto.VlTemperatura,
            VlUmidade = dto.VlUmidade,
            DtLeitura = dto.DtLeitura
        };

        await _leituraRepository.AddAsync(leitura);

        AlertaTemperatura? alerta = null;
        if (dto.VlTemperatura > _tempMax)
        {
            alerta = new AlertaTemperatura
            {
                DsTipoAlerta = "ACIMA_LIMITE",
                VlLimite = _tempMax,
                DsMensagem = $"Temperatura {dto.VlTemperatura:F1}°C acima do limite máximo de {_tempMax:F1}°C.",
                StResolvido = 'N'
            };
        }
        else if (dto.VlTemperatura < _tempMin)
        {
            alerta = new AlertaTemperatura
            {
                DsTipoAlerta = "ABAIXO_LIMITE",
                VlLimite = _tempMin,
                DsMensagem = $"Temperatura {dto.VlTemperatura:F1}°C abaixo do limite mínimo de {_tempMin:F1}°C.",
                StResolvido = 'N'
            };
        }

        await _uow.CommitAsync();

        if (alerta is not null)
        {
            alerta.IdLeituraTemperatura = leitura.Id;
            await _alertaRepository.AddAsync(alerta);
            await _uow.CommitAsync();
        }

        return ToResponse(leitura);
    }

    public async Task<IEnumerable<LeituraTemperaturaResponseDto>> GetByDispositivoAsync(long idDispositivo)
    {
        var leituras = await _leituraRepository.FindAsync(l => l.IdDispositivoIot == idDispositivo);
        return leituras.Select(ToResponse);
    }

    private static LeituraTemperaturaResponseDto ToResponse(LeituraTemperatura l) => new()
    {
        Id = l.Id,
        IdDispositivoIot = l.IdDispositivoIot,
        VlTemperatura = l.VlTemperatura,
        VlUmidade = l.VlUmidade,
        DtLeitura = l.DtLeitura
    };
}
