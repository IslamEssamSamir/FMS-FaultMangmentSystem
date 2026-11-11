using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IncidentManagementSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentAction> IncidentActions => Set<IncidentAction>();
    public DbSet<IncidentDepartmentAssignment> IncidentDepartmentAssignments => Set<IncidentDepartmentAssignment>();
    public DbSet<IncidentDepartmentAssignmentHistory> IncidentDepartmentAssignmentHistories => Set<IncidentDepartmentAssignmentHistory>();
    public DbSet<IncidentDepartmentAction> IncidentDepartmentActions => Set<IncidentDepartmentAction>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<IncidentTransferRequest> IncidentTransferRequests => Set<IncidentTransferRequest>();
    public DbSet<IncidentTransfer> IncidentTransfers => Set<IncidentTransfer>();
    public DbSet<IncidentStatusHistory> IncidentStatusHistories => Set<IncidentStatusHistory>();
    public DbSet<IncidentReview> IncidentReviews => Set<IncidentReview>();
    public DbSet<WeeklyShiftSchedule> WeeklyShiftSchedules => Set<WeeklyShiftSchedule>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<ShiftDefinition> ShiftDefinitions => Set<ShiftDefinition>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
