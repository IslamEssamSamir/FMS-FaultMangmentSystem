using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> builder)
    {
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.ShiftDate)
            .HasColumnType("date");
        builder.HasIndex(assignment => new
            {
                assignment.WeeklyShiftScheduleId,
                assignment.DepartmentId,
                assignment.ShiftDate,
                assignment.ShiftDefinitionId
            })
            .IsUnique();
        builder.HasIndex(assignment => new { assignment.WeeklyShiftScheduleId, assignment.UserId, assignment.ShiftDate });
        builder.HasOne(assignment => assignment.WeeklyShiftSchedule)
            .WithMany()
            .HasForeignKey(assignment => assignment.WeeklyShiftScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.Department)
            .WithMany()
            .HasForeignKey(assignment => assignment.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.User)
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.ShiftDefinition)
            .WithMany()
            .HasForeignKey(assignment => assignment.ShiftDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
