namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Interfaces;
using Kura.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

public class TimelineRepository : ITimelineRepository
{
    private readonly KuraDbContext _context;

    public TimelineRepository(KuraDbContext context) => _context = context;

    public async Task<IEnumerable<TimelineEntry>> GetByPetIdAsync(long idPet)
    {
        var items = await _context.TimelineItems
            .FromSqlRaw("SELECT * FROM VW_TIMELINE_PET WHERE ID_PET = {0}", idPet)
            .OrderByDescending(t => t.DtEvento)
            .ToListAsync();

        return items.Select(i => new TimelineEntry(
            i.IdEventoClinico,
            i.IdPet,
            i.NmPet,
            i.NmTipo,
            i.DtEvento,
            i.DsObservacao,
            i.NmVeterinario));
    }
}
