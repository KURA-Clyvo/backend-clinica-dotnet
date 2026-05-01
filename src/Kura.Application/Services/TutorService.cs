namespace Kura.Application.Services;

using Kura.Application.DTOs.Tutor;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class TutorService : ITutorService
{
    private readonly IRepository<Tutor> _repository;
    private readonly IUnitOfWork _uow;

    public TutorService(IRepository<Tutor> repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    public async Task<IEnumerable<TutorResponseDto>> GetAllAsync()
    {
        var tutores = await _repository.GetAllAsync();
        return tutores.Select(ToResponse);
    }

    public async Task<TutorResponseDto> GetByIdAsync(long id)
    {
        var tutor = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);
        return ToResponse(tutor);
    }

    public async Task<TutorResponseDto> CreateAsync(TutorCreateDto dto)
    {
        var tutor = new Tutor
        {
            NmTutor = dto.NmTutor,
            NrCpf = dto.NrCpf,
            DsEmail = dto.DsEmail,
            NrTelefone = dto.NrTelefone
        };
        await _repository.AddAsync(tutor);
        await _uow.CommitAsync();
        return ToResponse(tutor);
    }

    public async Task<TutorResponseDto> UpdateAsync(long id, TutorUpdateDto dto)
    {
        var tutor = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);

        tutor.NmTutor = dto.NmTutor;
        tutor.NrCpf = dto.NrCpf;
        tutor.DsEmail = dto.DsEmail;
        tutor.NrTelefone = dto.NrTelefone;
        tutor.DtAtualizacao = DateTime.UtcNow;

        _repository.Update(tutor);
        await _uow.CommitAsync();
        return ToResponse(tutor);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var tutor = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);
        _repository.SoftDelete(tutor);
        await _uow.CommitAsync();
    }

    private static TutorResponseDto ToResponse(Tutor t) => new()
    {
        Id = t.Id,
        NmTutor = t.NmTutor,
        NrCpf = t.NrCpf,
        DsEmail = t.DsEmail,
        NrTelefone = t.NrTelefone,
        StAtiva = t.StAtiva
    };
}
