namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IPetRepository : IRepository<Pet>
{
    Task<IEnumerable<Pet>> GetByFiltersAsync(long? tutorId, long? especieId, char? porte);
    Task<Pet?> GetByIdWithTutoresAsync(long id);

    /// <summary>
    /// REC-10 — busca por PK com <c>ID_CLINICA</c> EXPLÍCITO no predicado (A-7: "todo
    /// endpoint novo escopa por IClinicaContext à mão"), com <c>TutorPets</c> carregado para
    /// que o service verifique o vínculo pet↔tutor sem round trip extra. Deliberadamente NÃO
    /// usa <c>IRepository&lt;Pet&gt;.GetByIdAsync(id)</c> (herdado de <c>Repository&lt;T&gt;</c>,
    /// implementado com <c>DbSet.FindAsync</c>) -- <c>FindAsync</c> ignora
    /// <c>HasQueryFilter</c> por design do EF Core, então um pet de outra clínica passaria.
    /// Devolve <see langword="null"/> tanto para id inexistente quanto para pet de outra
    /// clínica -- mesma resposta, sem oráculo (REC-10, aceite).
    /// </summary>
    Task<Pet?> GetByIdComVinculosAsync(long id, long idClinica);
}
