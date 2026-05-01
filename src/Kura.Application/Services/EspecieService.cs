namespace Kura.Application.Services;

using Kura.Application.DTOs.Especie;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class EspecieService : IEspecieService
{
    private readonly IRepository<Especie> _repository;
    private readonly IUnitOfWork _uow;

    public EspecieService(IRepository<Especie> repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    public async Task<IEnumerable<EspecieResponseDto>> GetAllAsync()
    {
        var especies = await _repository.GetAllAsync();
        return especies.Select(ToResponse);
    }

    public async Task<EspecieResponseDto> GetByIdAsync(long id)
    {
        var especie = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Especie", id);
        return ToResponse(especie);
    }

    public async Task<EspecieResponseDto> CreateAsync(EspecieCreateDto dto)
    {
        var especie = new Especie
        {
            NmEspecie = dto.NmEspecie
        };
        await _repository.AddAsync(especie);
        await _uow.CommitAsync();
        return ToResponse(especie);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var especie = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Especie", id);
        _repository.SoftDelete(especie);
        await _uow.CommitAsync();
    }

    private static EspecieResponseDto ToResponse(Especie e) => new()
    {
        Id = e.Id,
        NmEspecie = e.NmEspecie,
        StAtiva = e.StAtiva
    };
}
