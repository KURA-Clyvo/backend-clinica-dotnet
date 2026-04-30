namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IRepository<T> where T : EntidadeBase
{
    Task<T?> GetByIdAsync(long id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    void Update(T entity);
    void SoftDelete(T entity); // sets StAtiva = 'N', never removes from database
}
