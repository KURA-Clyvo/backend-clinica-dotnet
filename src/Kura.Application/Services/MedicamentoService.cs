namespace Kura.Application.Services;

using Kura.Application.DTOs.Medicamento;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class MedicamentoService : IMedicamentoService
{
    private readonly IRepository<Medicamento> _repository;
    private readonly IUnitOfWork _uow;

    public MedicamentoService(IRepository<Medicamento> repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    public async Task<IEnumerable<MedicamentoResponseDto>> GetAllAsync()
    {
        var medicamentos = await _repository.GetAllAsync();
        return medicamentos.Select(ToResponse);
    }

    public async Task<MedicamentoResponseDto> GetByIdAsync(long id)
    {
        var medicamento = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Medicamento", id);
        return ToResponse(medicamento);
    }

    public async Task<MedicamentoResponseDto> CreateAsync(MedicamentoCreateDto dto)
    {
        var medicamento = new Medicamento
        {
            NmMedicamento = dto.NmMedicamento,
            DsPrincipioAtivo = dto.DsPrincipioAtivo,
            DsApresentacao = dto.DsApresentacao
        };
        await _repository.AddAsync(medicamento);
        await _uow.CommitAsync();
        return ToResponse(medicamento);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var medicamento = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Medicamento", id);
        _repository.SoftDelete(medicamento);
        await _uow.CommitAsync();
    }

    private static MedicamentoResponseDto ToResponse(Medicamento m) => new()
    {
        Id = m.Id,
        NmMedicamento = m.NmMedicamento,
        DsPrincipioAtivo = m.DsPrincipioAtivo,
        DsApresentacao = m.DsApresentacao,
        StAtiva = m.StAtiva
    };
}
