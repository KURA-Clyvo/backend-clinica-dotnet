namespace Kura.Application.Services;

using Kura.Application.DTOs.Clinica;
using Kura.Application.DTOs.Veterinario;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class ClinicaService : IClinicaService
{
    private readonly IClinicaRepository _repository;
    private readonly IVeterinarioRepository _vetRepository;
    private readonly IUnitOfWork _uow;

    public ClinicaService(
        IClinicaRepository repository,
        IVeterinarioRepository vetRepository,
        IUnitOfWork uow)
    {
        _repository = repository;
        _vetRepository = vetRepository;
        _uow = uow;
    }

    public async Task<IEnumerable<ClinicaResponseDto>> GetAllAsync()
    {
        var clinicas = await _repository.GetAllAsync();
        var result = new List<ClinicaResponseDto>();
        foreach (var clinica in clinicas)
        {
            var vets = await _vetRepository.GetAllByClinicaIdAsync(clinica.Id);
            result.Add(ToResponse(clinica, vets));
        }
        return result;
    }

    public async Task<ClinicaResponseDto> GetByIdAsync(long id)
    {
        var clinica = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Clinica", id);
        var vets = await _vetRepository.GetAllByClinicaIdAsync(id);
        return ToResponse(clinica, vets);
    }

    public async Task<ClinicaResponseDto> CreateAsync(ClinicaCreateDto dto)
    {
        var cnpjDigitos = new string(dto.NrCnpj.Where(char.IsDigit).ToArray());
        var clinica = new Clinica
        {
            NmClinica = dto.NmClinica,
            NrCnpj = cnpjDigitos,
            DsEndereco = dto.DsEndereco,
            NrTelefone = dto.NrTelefone,
            DsEmail = dto.DsEmail
        };
        await _repository.AddAsync(clinica);
        await _uow.CommitAsync();
        return ToResponse(clinica, []);
    }

    public async Task<ClinicaResponseDto> UpdateAsync(long id, ClinicaUpdateDto dto)
    {
        var clinica = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Clinica", id);

        clinica.NmClinica = dto.NmClinica;
        clinica.DsEndereco = dto.DsEndereco;
        clinica.NrTelefone = dto.NrTelefone;
        clinica.DsEmail = dto.DsEmail;

        _repository.Update(clinica);
        await _uow.CommitAsync();

        var vets = await _vetRepository.GetAllByClinicaIdAsync(id);
        return ToResponse(clinica, vets);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var clinica = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Clinica", id);
        _repository.SoftDelete(clinica);
        await _uow.CommitAsync();
    }

    private static ClinicaResponseDto ToResponse(Clinica c, IEnumerable<Veterinario> vets) => new()
    {
        Id = c.Id,
        NmClinica = c.NmClinica,
        NrCnpj = c.NrCnpj,
        DsEndereco = c.DsEndereco,
        NrTelefone = c.NrTelefone,
        DsEmail = c.DsEmail,
        StAtiva = c.StAtiva,
        Veterinarios = vets.Select(v => new VeterinarioResponseDto
        {
            Id = v.Id,
            IdClinica = v.IdClinica,
            NmVeterinario = v.NmVeterinario,
            NrCrmv = v.NrCrmv,
            DsEmail = v.DsEmail,
            NrTelefone = v.NrTelefone,
            StAtiva = v.StAtiva
        }).ToList()
    };
}
