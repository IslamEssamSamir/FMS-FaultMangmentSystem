using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class WeeklyShiftScheduleConfiguration : IEntityTypeConfiguration<WeeklyShiftSchedule>
{
    public void Configure(EntityTypeBuilder<WeeklyShiftSchedule> builder)
    {
        builder.HasKey(schedule => schedule.Id);
        builder.Property(schedule => schedule.WeekStartDate)
            .HasColumnType("date");
        builder.Property(schedule => schedule.CreatedAt)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(schedule => schedule.WeekStartDate)
            .IsUnique();
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_WeeklyShiftSchedules_WeekStartDate_Saturday",
            "DATEDIFF(day, CONVERT(date, '19000106', 112), [WeekStartDate]) % 7 = 0"));
        builder.HasOne(schedule => schedule.CreatedByUser)
            .WithMany()
            .HasForeignKey(schedule => schedule.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
