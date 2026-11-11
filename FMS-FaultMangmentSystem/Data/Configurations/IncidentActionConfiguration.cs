using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class IncidentActionConfiguration : IEntityTypeConfiguration<IncidentAction>
{
    public void Configure(EntityTypeBuilder<IncidentAction> builder)
    {
        builder.HasKey(action => action.Id);
        builder.Property(action => action.ActionDescription)
            .HasMaxLength(4000)
            .IsRequired();
        builder.Property(action => action.ActionDate)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(action => new { action.IncidentId, action.ActionDate });
        builder.HasIndex(action => action.DepartmentId);
        builder.HasOne(action => action.Incident)
            .WithMany()
            .HasForeignKey(action => action.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(action => action.Department)
            .WithMany()
            .HasForeignKey(action => action.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(action => action.User)
            .WithMany()
            .HasForeignKey(action => action.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
