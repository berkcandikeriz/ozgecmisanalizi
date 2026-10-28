using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OzgecmisAnalizi.DataAccess.Context;
using OzgecmisAnalizi.Entities.Common;

namespace OzgecmisAnalizi.DataAccess.Repositories;

public class EfRepository<T>(AppDbContext context) : IRepository<T> where T : BaseEntity
{
    private readonly DbSet<T> _set = context.Set<T>();

    public IQueryable<T> Query() => _set.AsNoTracking();
    public IQueryable<T> Tracked() => _set;

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _set.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        _set.AnyAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) => await _set.AddAsync(entity, ct);

    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) => _set.AddRangeAsync(entities, ct);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);
}

public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = [];

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        if (!_repositories.TryGetValue(typeof(T), out var repo))
        {
            repo = new EfRepository<T>(context);
            _repositories[typeof(T)] = repo;
        }

        return (IRepository<T>)repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
