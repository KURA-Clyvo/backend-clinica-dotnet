namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IEventoClinicoRepository : IRepository<EventoClinico>
{
    Task<IEnumerable<EventoClinico>> GetByFiltersAsync(
        long? petId, long? tipoEventoId, DateTime? dataInicio, DateTime? dataFim, long? veterinarioId);
}
