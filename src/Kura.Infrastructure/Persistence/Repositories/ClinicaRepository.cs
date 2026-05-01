namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;

public class ClinicaRepository : Repository<Clinica>, IClinicaRepository
{
    public ClinicaRepository(KuraDbContext context) : base(context)
    {
    }
}
