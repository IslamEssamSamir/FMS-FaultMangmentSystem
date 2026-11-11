using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class IncidentDepartmentAssignmentHistoryConfiguration : IEntityTypeConfiguration<IncidentDepartmentAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<IncidentDepartmentAssignmentHistory> builder)
    {
        builder.HasKey(history => history.Id);
        builder.Property(history => history.EventType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)");
        builder.Property(history => history.PreviousStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)");
        builder.Property(history => history.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)");
        builder.Property(history => history.ChangedAt)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(history => history.Notes).HasMaxLength(2000);
        builder.HasIndex(history => new { history.AssignmentId, history.ChangedAt });
        builder.HasOne(history => history.Assignment)
            .WithMany(assignment => assignment.History)
            .HasForeignKey(history => history.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(history => history.ChangedByUser)
            .WithMany()
            .HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}