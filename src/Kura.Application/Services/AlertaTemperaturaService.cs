namespace Kura.Application.Services;

using Kura.Application.DTOs.AlertaTemperatura;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class AlertaTemperaturaService : IAlertaTemperaturaService
{
    private readonly IRepository<AlertaTemperatura> _alertaRepository;
    private readonly IRepository<LeituraTemperatura> _leituraRepository;
    private readonly IUnitOfWork _uow;

    public AlertaTemperaturaService(
        IRepository<AlertaTemperatura> alertaRepository,
        IRepository<LeituraTemperatura> leituraRepository,
        IUnitOfWork uow)
    {
        _alertaRepository = alertaRepository;
        _leituraRepository = leituraRepository;
        _uow = uow;
    }

    public async Task<IEnumerable<AlertaTemperaturaResponseDto>> GetByDispositivoAsync(long idDispositivo)
    {
        var leituras = await _leituraRepository.FindAsync(l => l.IdDispositivoIot == idDispositivo);
        var leituraIds = leituras.Select(l => l.Id).ToHashSet();

        var alertas = await _alertaRepository.FindAsync(a => leituraIds.Contains(a.IdLeituraTemperatura));
        return alertas.Select(ToResponse);
    }

    public async Task ResolverAsync(long id)
    {
        var alerta = await _alertaRepository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("AlertaTemperatura", id);

        alerta.StResolvido = 'S';
        alerta.DtAtualizacao = DateTime.UtcNow;

        _alertaRepository.Update(alerta);
        await _uow.CommitAsync();
    }

    private static AlertaTemperaturaResponseDto ToResponse(AlertaTemperatura a) => new()
    {
        Id = a.Id,
        IdLeituraTemperatura = a.IdLeituraTemperatura,
        DsTipoAlerta = a.DsTipoAlerta,
        VlLimite = a.VlLimite,
        DsMensagem = a.DsMensagem,
        StResolvido = a.StResolvido
    };
}
