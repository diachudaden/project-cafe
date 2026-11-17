using ChonRieng.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Infrastructure.Persistence;

public sealed class EfEntityCrudService<TEntity>(AppDbContext dbContext) : IEntityCrudService<TEntity>
    where TEntity : class
{
    private DbSet<TEntity> Entities => dbContext.Set<TEntity>();

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return await Entities
            .AsNoTracking()
            .OrderBy(entity => EF.Property<Guid>(entity, "Id"))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
    }

    public Task<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return Entities
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => EF.Property<Guid>(entity, "Id") == id, cancellationToken);
    }

    public async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        Entities.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<TEntity?> UpdateAsync(
        Guid id,
        Action<TEntity> update,
        CancellationToken cancellationToken)
    {
        var entity = await Entities.SingleOrDefaultAsync(
            item => EF.Property<Guid>(item, "Id") == id,
            cancellationToken);
        if (entity is null)
        {
            return null;
        }

        update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await Entities.SingleOrDefaultAsync(
            item => EF.Property<Guid>(item, "Id") == id,
            cancellationToken);
        if (entity is null)
        {
            return false;
        }

        Entities.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
