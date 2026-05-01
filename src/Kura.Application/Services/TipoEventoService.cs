namespace Kura.Application.Services;

using Kura.Application.DTOs.TipoEvento;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class TipoEventoService : ITipoEventoService
{
    private readonly IRepository<TipoEvento> _repository;

    public TipoEventoService(IRepository<TipoEvento> repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<TipoEventoResponseDto>> GetAllAsync()
    {
        var tiposEvento = await _repository.GetAllAsync();
        return tiposEvento.Select(ToResponse);
    }

    public async Task<TipoEventoResponseDto> GetByIdAsync(long id)
    {
        var tipoEvento = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("TipoEvento", id);
        return ToResponse(tipoEvento);
    }

    private static TipoEventoResponseDto ToResponse(TipoEvento t) => new()
    {
        Id = t.Id,
        CdTipo = t.CdTipo,
        NmTipo = t.NmTipo
    };
}
