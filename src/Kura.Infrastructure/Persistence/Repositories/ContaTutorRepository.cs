namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class ContaTutorRepository(KuraDbContext context) : IContaTutorRepository
{
    public Task<bool> ExisteContaAsync(long idTutor)
        => context.ContasTutor.AnyAsync(c => c.IdTutor == idTutor);
}
