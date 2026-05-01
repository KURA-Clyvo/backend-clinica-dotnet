namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class EventoClinicoRepository : Repository<EventoClinico>, IEventoClinicoRepository
{
    public EventoClinicoRepository(KuraDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EventoClinico>> GetByFiltersAsync(
        long? petId, long? tipoEventoId, DateTime? dataInicio, DateTime? dataFim, long? veterinarioId)
    {
        var query = _dbSet.AsQueryable();

        if (petId.HasValue)
            query = query.Where(e => e.IdPet == petId.Value);

        if (tipoEventoId.HasValue)
            query = query.Where(e => e.IdTipoEvento == tipoEventoId.Value);

        if (dataInicio.HasValue)
            query = query.Where(e => e.DtEvento >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(e => e.DtEvento <= dataFim.Value);

        if (veterinarioId.HasValue)
            query = query.Where(e => e.IdVeterinario == veterinarioId.Value);

        return await query.OrderByDescending(e => e.DtEvento).ToListAsync();
    }
}
