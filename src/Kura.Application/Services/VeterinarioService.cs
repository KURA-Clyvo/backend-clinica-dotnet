namespace Kura.Application.Services;

using Kura.Application.DTOs.Veterinario;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class VeterinarioService : IVeterinarioService
{
    private readonly IVeterinarioRepository _repository;
    private readonly IUnitOfWork _uow;

    public VeterinarioService(IVeterinarioRepository repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    public async Task<IEnumerable<VeterinarioResponseDto>> GetAllAsync()
    {
        var veterinarios = await _repository.GetAllAsync();
        return veterinarios.Select(ToResponse);
    }

    public async Task<VeterinarioResponseDto> GetByIdAsync(long id)
    {
        var veterinario = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Veterinario", id);
        return ToResponse(veterinario);
    }

    public async Task<IEnumerable<VeterinarioResponseDto>> GetByClinicaAsync(long idClinica)
    {
        var veterinarios = await _repository.GetAllByClinicaIdAsync(idClinica);
        return veterinarios.Select(ToResponse);
    }

    public async Task<VeterinarioResponseDto> CreateAsync(VeterinarioCreateDto dto)
    {
        var veterinario = new Veterinario
        {
            IdClinica = dto.IdClinica,
            NmVeterinario = dto.NmVeterinario,
            NrCrmv = dto.NrCrmv,
            DsEmail = dto.DsEmail,
            NrTelefone = dto.NrTelefone
        };
        await _repository.AddAsync(veterinario);
        await _uow.CommitAsync();
        return ToResponse(veterinario);
    }

    public async Task<VeterinarioResponseDto> UpdateAsync(long id, VeterinarioUpdateDto dto)
    {
        var veterinario = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Veterinario", id);

        veterinario.NmVeterinario = dto.NmVeterinario;
        veterinario.NrCrmv = dto.NrCrmv;
        veterinario.DsEmail = dto.DsEmail;
        veterinario.NrTelefone = dto.NrTelefone;

        _repository.Update(veterinario);
        await _uow.CommitAsync();
        return ToResponse(veterinario);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var veterinario = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Veterinario", id);
        _repository.SoftDelete(veterinario);
        await _uow.CommitAsync();
    }

    private static VeterinarioResponseDto ToResponse(Veterinario v) => new()
    {
        Id = v.Id,
        IdClinica = v.IdClinica,
        NmVeterinario = v.NmVeterinario,
        NrCrmv = v.NrCrmv,
        DsEmail = v.DsEmail,
        NrTelefone = v.NrTelefone,
        StAtiva = v.StAtiva
    };
}
