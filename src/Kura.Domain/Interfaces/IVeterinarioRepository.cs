namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IVeterinarioRepository : IRepository<Veterinario>
{
    Task<IEnumerable<Veterinario>> GetAllByClinicaIdAsync(long idClinica);
}
