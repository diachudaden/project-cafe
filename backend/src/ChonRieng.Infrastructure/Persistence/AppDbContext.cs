using ChonRieng.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<PricePlan> PricePlans => Set<PricePlan>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<ShiftClosing> ShiftClosings => Set<ShiftClosing>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionExtension> SessionExtensions => Set<SessionExtension>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillItem> BillItems => Set<BillItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PaymentRequest> PaymentRequests => Set<PaymentRequest>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PaymentLog> PaymentLogs => Set<PaymentLog>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<EmployeeBranch> EmployeeBranches => Set<EmployeeBranch>();
    public DbSet<Availability> Availabilities => Set<Availability>();
    public DbSet<ShiftRequest> ShiftRequests => Set<ShiftRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
