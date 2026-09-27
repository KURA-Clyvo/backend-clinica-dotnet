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

    /// <inheritdoc />
    public Task<Veterinario?> BuscarPorIdIgnorandoFiltrosAsync(long id) =>
        _dbSet.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.Id == id);

    /// <inheritdoc />
    public Task<Veterinario?> GetByIdAsync(long id, long idClinica) =>
        _dbSet.FirstOrDefaultAsync(v => v.Id == id && v.IdClinica == idClinica);
}
