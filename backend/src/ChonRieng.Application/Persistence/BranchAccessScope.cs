using System.Linq.Expressions;

namespace ChonRieng.Application.Persistence;

public sealed record BranchAccessScope(bool IsGlobal, IReadOnlySet<Guid> BranchIds)
{
    public static BranchAccessScope Global { get; } = new(true, new HashSet<Guid>());

    public static BranchAccessScope ForBranches(IReadOnlySet<Guid> branchIds)
    {
        return new(false, branchIds);
    }

    public IQueryable<TEntity> Apply<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<TEntity, Guid>> branchIdSelector)
    {
        if (IsGlobal)
        {
            return source;
        }

        var contains = typeof(IReadOnlySet<Guid>).GetMethod(nameof(IReadOnlySet<Guid>.Contains))
            ?? throw new InvalidOperationException("The branch set does not expose a Contains method.");
        var predicate = Expression.Call(
            Expression.Constant(BranchIds),
            contains,
            branchIdSelector.Body);
        return source.Where(Expression.Lambda<Func<TEntity, bool>>(predicate, branchIdSelector.Parameters));
    }
}
