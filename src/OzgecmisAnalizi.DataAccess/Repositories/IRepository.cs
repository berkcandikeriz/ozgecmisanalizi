using System.Linq.Expressions;
using OzgecmisAnalizi.Entities.Common;

namespace OzgecmisAnalizi.DataAccess.Repositories;

public interface IRepository<T> where T : BaseEntity
{
    /// <summary>Takip edilmeyen (salt okunur) sorgu.</summary>
    IQueryable<T> Query();

    /// <summary>Değişiklik takibi açık sorgu (güncelleme için).</summary>
    IQueryable<T> Tracked();

    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    void Update(T entity);

    /// <summary>Yumuşak siler (IsDeleted = true).</summary>
    void Remove(T entity);
}

public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
