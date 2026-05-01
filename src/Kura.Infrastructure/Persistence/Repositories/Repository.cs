namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class Repository<T> : IRepository<T> where T : EntidadeBase
{
    protected readonly KuraDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(KuraDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public Task<T?> GetByIdAsync(long id)
    {
        return _dbSet.FindAsync(id).AsTask();
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public void Update(T entity)
    {
        entity.DtAtualizacao = DateTime.UtcNow;
        _dbSet.Update(entity);
    }

    public void SoftDelete(T entity)
    {
        entity.StAtiva = 'N';
        entity.DtAtualizacao = DateTime.UtcNow;
        _dbSet.Update(entity);
    }
}
