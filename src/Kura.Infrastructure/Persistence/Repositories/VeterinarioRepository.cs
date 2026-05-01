namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class VeterinarioRepository : Repository<Veterinario>, IVeterinarioRepository
{
    public VeterinarioRepository(KuraDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Veterinario>> GetAllByClinicaIdAsync(long idClinica)
    {
        return await _dbSet.Where(v => v.IdClinica == idClinica).ToListAsync();
    }
}
