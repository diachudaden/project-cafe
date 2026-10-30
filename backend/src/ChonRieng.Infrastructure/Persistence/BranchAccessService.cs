using ChonRieng.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Infrastructure.Persistence;

public sealed class BranchAccessService(AppDbContext dbContext) : IBranchAccessService
{
    public async Task<IReadOnlySet<Guid>> GetAssignedBranchIdsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.EmployeeBranches
            .AsNoTracking()
            .Where(assignment => assignment.Employee.UserId == userId)
            .Select(assignment => assignment.BranchId)
            .ToHashSetAsync(cancellationToken);
    }

    public Task<bool> IsCustomerOwnedByUserAsync(
        Guid customerId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .AsNoTracking()
            .AnyAsync(customer => customer.Id == customerId && customer.UserId == userId, cancellationToken);
    }
}
