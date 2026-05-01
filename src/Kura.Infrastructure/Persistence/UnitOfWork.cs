namespace Kura.Infrastructure.Persistence;

using Kura.Domain.Interfaces;

public class UnitOfWork : IUnitOfWork
{
    private readonly KuraDbContext _context;

    public UnitOfWork(KuraDbContext context)
    {
        _context = context;
    }

    public Task<int> CommitAsync()
    {
        return _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        // No-op: the DbContext lifetime is managed by DI (scoped).
    }
}
