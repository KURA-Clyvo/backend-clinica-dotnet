namespace Kura.Application.Services;

using Kura.Application.DTOs.Raca;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class RacaService : IRacaService
{
    private readonly IRepository<Raca> _repository;
    private readonly IRepository<Especie> _especieRepository;
    private readonly IUnitOfWork _uow;

    public RacaService(
        IRepository<Raca> repository,
        IRepository<Especie> especieRepository,
        IUnitOfWork uow)
    {
        _repository = repository;
        _especieRepository = especieRepository;
        _uow = uow;
    }

    public async Task<IEnumerable<RacaResponseDto>> GetAllAsync()
    {
        var racas = await _repository.GetAllAsync();
        return await MapManyAsync(racas);
    }

    public async Task<RacaResponseDto> GetByIdAsync(long id)
    {
        var raca = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Raca", id);
        return await MapAsync(raca);
    }

    public async Task<IEnumerable<RacaResponseDto>> GetByEspecieAsync(long idEspecie)
    {
        var racas = await _repository.FindAsync(r => r.IdEspecie == idEspecie);
        return await MapManyAsync(racas);
    }

    public async Task<RacaResponseDto> CreateAsync(RacaCreateDto dto)
    {
        var especie = await _especieRepository.GetByIdAsync(dto.IdEspecie)
            ?? throw new EntidadeNaoEncontradaException("Especie", dto.IdEspecie);

        var raca = new Raca
        {
            IdEspecie = dto.IdEspecie,
            NmRaca = dto.NmRaca
        };
        await _repository.AddAsync(raca);
        await _uow.CommitAsync();

        return new RacaResponseDto
        {
            Id = raca.Id,
            IdEspecie = raca.IdEspecie,
            NmEspecie = especie.NmEspecie,
            NmRaca = raca.NmRaca,
            StAtiva = raca.StAtiva
        };
    }

    public async Task SoftDeleteAsync(long id)
    {
        var raca = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Raca", id);
        _repository.SoftDelete(raca);
        await _uow.CommitAsync();
    }

    private async Task<RacaResponseDto> MapAsync(Raca raca)
    {
        var especie = await _especieRepository.GetByIdAsync(raca.IdEspecie);
        return new RacaResponseDto
        {
            Id = raca.Id,
            IdEspecie = raca.IdEspecie,
            NmEspecie = especie?.NmEspecie ?? string.Empty,
            NmRaca = raca.NmRaca,
            StAtiva = raca.StAtiva
        };
    }

    private async Task<IEnumerable<RacaResponseDto>> MapManyAsync(IEnumerable<Raca> racas)
    {
        var result = new List<RacaResponseDto>();
        foreach (var raca in racas)
        {
            result.Add(await MapAsync(raca));
        }
        return result;
    }
}
