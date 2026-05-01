namespace Kura.Application.Services;

using Kura.Application.DTOs.EventoClinico;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class EventoClinicoService : IEventoClinicoService
{
    private readonly IRepository<EventoClinico> _repository;
    private readonly IRepository<TipoEvento> _tipoEventoRepository;

    public EventoClinicoService(
        IRepository<EventoClinico> repository,
        IRepository<TipoEvento> tipoEventoRepository)
    {
        _repository = repository;
        _tipoEventoRepository = tipoEventoRepository;
    }

    public async Task<EventoClinicoResponseDto> GetByIdAsync(long id)
    {
        var evento = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("EventoClinico", id);
        return await BuildResponseAsync(evento);
    }

    public async Task<IEnumerable<EventoClinicoResponseDto>> GetByPetAsync(long idPet)
    {
        var eventos = await _repository.FindAsync(e => e.IdPet == idPet);
        var result = new List<EventoClinicoResponseDto>();
        foreach (var evento in eventos)
        {
            result.Add(await BuildResponseAsync(evento));
        }
        return result;
    }

    private async Task<EventoClinicoResponseDto> BuildResponseAsync(EventoClinico evento)
    {
        var tipoEvento = await _tipoEventoRepository.GetByIdAsync(evento.IdTipoEvento);
        return new EventoClinicoResponseDto
        {
            Id = evento.Id,
            IdPet = evento.IdPet,
            IdVeterinario = evento.IdVeterinario,
            IdTipoEvento = evento.IdTipoEvento,
            NmTipoEvento = tipoEvento?.NmTipo ?? string.Empty,
            DtEvento = evento.DtEvento,
            DsObservacao = evento.DsObservacao,
            StAtiva = evento.StAtiva
        };
    }
}
