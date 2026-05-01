namespace Kura.Application.Services;

using Kura.Application.DTOs.DispositivoIot;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class DispositivoIotService : IDispositivoIotService
{
    private readonly IRepository<DispositivoIot> _repository;
    private readonly IUnitOfWork _uow;

    public DispositivoIotService(IRepository<DispositivoIot> repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    public async Task<IEnumerable<DispositivoIotResponseDto>> GetByClinicaAsync(long idClinica)
    {
        var dispositivos = await _repository.FindAsync(d => d.IdClinica == idClinica);
        return dispositivos.Select(ToResponse);
    }

    public async Task<DispositivoIotResponseDto> GetByIdAsync(long id)
    {
        var dispositivo = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("DispositivoIot", id);
        return ToResponse(dispositivo);
    }

    public async Task<DispositivoIotResponseDto> CreateAsync(DispositivoIotCreateDto dto)
    {
        var dispositivo = new DispositivoIot
        {
            IdClinica = dto.IdClinica,
            CdDispositivo = dto.CdDispositivo,
            DsDescricao = dto.DsDescricao,
            DsLocalizacao = dto.DsLocalizacao
        };
        await _repository.AddAsync(dispositivo);
        await _uow.CommitAsync();
        return ToResponse(dispositivo);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var dispositivo = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("DispositivoIot", id);
        _repository.SoftDelete(dispositivo);
        await _uow.CommitAsync();
    }

    private static DispositivoIotResponseDto ToResponse(DispositivoIot d) => new()
    {
        Id = d.Id,
        IdClinica = d.IdClinica,
        CdDispositivo = d.CdDispositivo,
        DsDescricao = d.DsDescricao,
        DsLocalizacao = d.DsLocalizacao,
        StAtiva = d.StAtiva
    };
}
