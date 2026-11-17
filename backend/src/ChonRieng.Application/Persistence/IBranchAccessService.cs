namespace ChonRieng.Application.Persistence;

public interface IBranchAccessService
{
    Task<IReadOnlySet<Guid>> GetAssignedBranchIdsAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> IsCustomerOwnedByUserAsync(Guid customerId, Guid userId, CancellationToken cancellationToken);
}
