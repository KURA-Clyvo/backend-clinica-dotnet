namespace Kura.Application.Services;

using Kura.Application.DTOs.Clinica;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class ClinicaService : IClinicaService
{
    private readonly IRepository<Clinica> _repository;
    private readonly IUnitOfWork _uow;

    public ClinicaService(IRepository<Clinica> repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    public async Task<ClinicaResponseDto> GetByIdAsync(long id)
    {
        var clinica = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Clinica", id);
        return ToResponse(clinica);
    }

    public async Task<ClinicaResponseDto> UpdateAsync(long id, ClinicaUpdateDto dto)
    {
        var clinica = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Clinica", id);

        clinica.NmClinica = dto.NmClinica;
        clinica.DsEndereco = dto.DsEndereco;
        clinica.NrTelefone = dto.NrTelefone;
        clinica.DsEmail = dto.DsEmail;
        clinica.DtAtualizacao = DateTime.UtcNow;

        _repository.Update(clinica);
        await _uow.CommitAsync();
        return ToResponse(clinica);
    }

    private static ClinicaResponseDto ToResponse(Clinica c) => new()
    {
        Id = c.Id,
        NmClinica = c.NmClinica,
        NrCnpj = c.NrCnpj,
        DsEndereco = c.DsEndereco,
        NrTelefone = c.NrTelefone,
        DsEmail = c.DsEmail,
        StAtiva = c.StAtiva
    };
}
