using System.Linq.Expressions;

namespace InventoryPro.Application.Interfaces;

public interface IGenericRepository<T> where T : class
{
    IQueryable<T> Query(bool asNoTracking = true);
    Task<T?> GetByIdAsync(int id);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}
