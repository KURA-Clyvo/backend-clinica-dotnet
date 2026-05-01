namespace Kura.Domain.Interfaces;

using System.Linq.Expressions;
using Kura.Domain.Entities;

public interface IRepository<T> where T : EntidadeBase
{
    Task<T?> GetByIdAsync(long id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    void Update(T entity);
    void SoftDelete(T entity); // sets StAtiva = 'N', never removes from database
}
