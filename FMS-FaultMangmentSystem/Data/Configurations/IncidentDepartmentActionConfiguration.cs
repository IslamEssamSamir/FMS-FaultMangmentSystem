using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class IncidentDepartmentActionConfiguration : IEntityTypeConfiguration<IncidentDepartmentAction>
{
    public void Configure(EntityTypeBuilder<IncidentDepartmentAction> builder)
    {
        builder.HasKey(action => action.Id);
        builder.Property(action => action.ActionDescription)
            .HasMaxLength(4000)
            .IsRequired();
        builder.Property(action => action.ActionDate)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(action => new { action.IncidentId, action.ActionDate });
        builder.HasOne(action => action.Incident)
            .WithMany(incident => incident.DepartmentActions)
            .HasForeignKey(action => action.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(action => action.Assignment)
            .WithMany(assignment => assignment.Actions)
            .HasForeignKey(action => new { action.AssignmentId, action.IncidentId })
            .HasPrincipalKey(assignment => new { assignment.Id, assignment.IncidentId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(action => action.User)
            .WithMany()
            .HasForeignKey(action => action.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}