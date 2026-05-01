namespace Kura.Application.Services;

using Kura.Application.DTOs.Pet;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class PetService : IPetService
{
    private readonly IRepository<Pet> _repository;
    private readonly IRepository<Especie> _especieRepository;
    private readonly IRepository<Raca> _racaRepository;
    private readonly IUnitOfWork _uow;

    public PetService(
        IRepository<Pet> repository,
        IRepository<Especie> especieRepository,
        IRepository<Raca> racaRepository,
        IUnitOfWork uow)
    {
        _repository = repository;
        _especieRepository = especieRepository;
        _racaRepository = racaRepository;
        _uow = uow;
    }

    public async Task<IEnumerable<PetResponseDto>> GetAllAsync()
    {
        var pets = await _repository.GetAllAsync();
        var result = new List<PetResponseDto>();
        foreach (var pet in pets)
        {
            result.Add(await BuildResponseAsync(pet));
        }
        return result;
    }

    public async Task<PetResponseDto> GetByIdAsync(long id)
    {
        var pet = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Pet", id);
        return await BuildResponseAsync(pet);
    }

    public async Task<PetResponseDto> CreateAsync(PetCreateDto dto)
    {
        var pet = new Pet
        {
            IdEspecie = dto.IdEspecie,
            IdRaca = dto.IdRaca,
            IdVeterinarioResp = dto.IdVeterinarioResp,
            NmPet = dto.NmPet,
            DtNascimento = dto.DtNascimento,
            SgSexo = dto.SgSexo,
            SgPorte = dto.SgPorte
        };
        await _repository.AddAsync(pet);
        await _uow.CommitAsync();
        return await BuildResponseAsync(pet);
    }

    public async Task<PetResponseDto> UpdateAsync(long id, PetUpdateDto dto)
    {
        var pet = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Pet", id);

        pet.IdVeterinarioResp = dto.IdVeterinarioResp;
        pet.NmPet = dto.NmPet;
        pet.SgSexo = dto.SgSexo;
        pet.SgPorte = dto.SgPorte;
        pet.DtAtualizacao = DateTime.UtcNow;

        _repository.Update(pet);
        await _uow.CommitAsync();
        return await BuildResponseAsync(pet);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var pet = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Pet", id);
        _repository.SoftDelete(pet);
        await _uow.CommitAsync();
    }

    private async Task<PetResponseDto> BuildResponseAsync(Pet pet)
    {
        var especie = await _especieRepository.GetByIdAsync(pet.IdEspecie);
        var raca = await _racaRepository.GetByIdAsync(pet.IdRaca);

        return new PetResponseDto
        {
            Id = pet.Id,
            NmPet = pet.NmPet,
            IdEspecie = pet.IdEspecie,
            NmEspecie = especie?.NmEspecie ?? string.Empty,
            IdRaca = pet.IdRaca,
            NmRaca = raca?.NmRaca ?? string.Empty,
            IdVeterinarioResp = pet.IdVeterinarioResp,
            DtNascimento = pet.DtNascimento,
            SgSexo = pet.SgSexo,
            SgPorte = pet.SgPorte,
            StAtiva = pet.StAtiva
        };
    }
}
