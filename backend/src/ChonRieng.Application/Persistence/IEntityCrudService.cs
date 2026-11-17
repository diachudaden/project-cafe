namespace ChonRieng.Application.Persistence;

public interface IEntityCrudService<TEntity>
    where TEntity : class
{
    Task<IReadOnlyList<TEntity>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken);

    Task<TEntity?> UpdateAsync(Guid id, Action<TEntity> update, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
