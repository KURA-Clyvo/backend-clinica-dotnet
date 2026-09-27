namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class PetRepository : Repository<Pet>, IPetRepository
{
    public PetRepository(KuraDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Pet>> GetByFiltersAsync(long? tutorId, long? especieId, char? porte)
    {
        var query = _dbSet.AsQueryable();

        if (tutorId.HasValue)
            query = query.Where(p => p.TutorPets.Any(tp => tp.IdTutor == tutorId.Value));

        if (especieId.HasValue)
            query = query.Where(p => p.IdEspecie == especieId.Value);

        if (porte.HasValue)
            query = query.Where(p => p.SgPorte == porte.Value);

        return await query.ToListAsync();
    }

    public async Task<Pet?> GetByIdWithTutoresAsync(long id)
    {
        return await _dbSet
            .Include(p => p.TutorPets)
                .ThenInclude(tp => tp.Tutor)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Pet?> GetByIdComVinculosAsync(long id, long idClinica)
    {
        return await _dbSet
            .Include(p => p.TutorPets)
            .FirstOrDefaultAsync(p => p.Id == id && p.IdClinica == idClinica);
    }
}
