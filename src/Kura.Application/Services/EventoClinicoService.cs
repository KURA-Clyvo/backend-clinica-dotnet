namespace Kura.Application.Services;

using Kura.Application.DTOs.EventoClinico;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class EventoClinicoService : IEventoClinicoService
{
    private readonly IEventoClinicoRepository _repository;
    private readonly IRepository<TipoEvento> _tipoEventoRepository;
    private readonly ITimelineRepository _timelineRepository;

    public EventoClinicoService(
        IEventoClinicoRepository repository,
        IRepository<TipoEvento> tipoEventoRepository,
        ITimelineRepository timelineRepository)
    {
        _repository = repository;
        _tipoEventoRepository = tipoEventoRepository;
        _timelineRepository = timelineRepository;
    }

    public async Task<EventoClinicoResponseDto> GetByIdAsync(long id)
    {
        var evento = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("EventoClinico", id);
        return await BuildResponseAsync(evento);
    }

    public async Task<IEnumerable<EventoClinicoResponseDto>> GetByFiltersAsync(
        long? petId, string? tipo, DateTime? dataInicio, DateTime? dataFim, long? veterinarioId)
    {
        long? tipoEventoId = null;
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            var tiposEvento = await _tipoEventoRepository.GetAllAsync();
            var tipoEncontrado = tiposEvento.FirstOrDefault(
                t => t.NmTipo.Equals(tipo, StringComparison.OrdinalIgnoreCase));
            tipoEventoId = tipoEncontrado?.Id;
        }

        var eventos = await _repository.GetByFiltersAsync(petId, tipoEventoId, dataInicio, dataFim, veterinarioId);
        var result = new List<EventoClinicoResponseDto>();
        foreach (var evento in eventos)
            result.Add(await BuildResponseAsync(evento));
        return result;
    }

    public async Task<IEnumerable<TimelineItemDto>> GetTimelineAsync(long idPet)
    {
        var entries = await _timelineRepository.GetByPetIdAsync(idPet);
        return entries.Select(e => new TimelineItemDto
        {
            IdEventoClinico = e.IdEventoClinico,
            IdPet = e.IdPet,
            NmPet = e.NmPet,
            NmTipo = e.NmTipo,
            DtEvento = e.DtEvento,
            DsObservacao = e.DsObservacao,
            NmVeterinario = e.NmVeterinario
        });
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
