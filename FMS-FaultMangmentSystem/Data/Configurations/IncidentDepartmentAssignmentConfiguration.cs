using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class IncidentDepartmentAssignmentConfiguration : IEntityTypeConfiguration<IncidentDepartmentAssignment>
{
    public void Configure(EntityTypeBuilder<IncidentDepartmentAssignment> builder)
    {
        builder.HasKey(assignment => assignment.Id);
        builder.HasAlternateKey(assignment => new { assignment.Id, assignment.IncidentId });
        builder.Property(assignment => assignment.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)")
            .HasDefaultValue(IncidentManagementSystem.Models.Enums.IncidentDepartmentTaskStatus.Pending);
        builder.Property(assignment => assignment.RequiredTask)
            .HasMaxLength(4000)
            .IsRequired();
        builder.Property(assignment => assignment.AssignedAt)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(assignment => assignment.CompletedAt).HasColumnType("datetime2(7)");
        builder.Property(assignment => assignment.RemovedAt).HasColumnType("datetime2(7)");
        builder.Property(assignment => assignment.RemovalReason).HasMaxLength(2000);
        builder.HasIndex(assignment => new { assignment.IncidentId, assignment.DepartmentId })
            .IsUnique()
            .HasFilter("[RemovedAt] IS NULL");
        builder.HasIndex(assignment => new { assignment.IncidentId, assignment.Status, assignment.RemovedAt });
        builder.HasOne(assignment => assignment.Incident)
            .WithMany(incident => incident.DepartmentAssignments)
            .HasForeignKey(assignment => assignment.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.Department)
            .WithMany()
            .HasForeignKey(assignment => assignment.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.AssignedByUser)
            .WithMany()
            .HasForeignKey(assignment => assignment.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.RemovedByUser)
            .WithMany()
            .HasForeignKey(assignment => assignment.RemovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}