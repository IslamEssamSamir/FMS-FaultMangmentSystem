using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class IncidentStatusHistoryConfiguration : IEntityTypeConfiguration<IncidentStatusHistory>
{
    public void Configure(EntityTypeBuilder<IncidentStatusHistory> builder)
    {
        builder.HasKey(history => history.Id);
        builder.Property(history => history.PreviousStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)");
        builder.Property(history => history.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)")
            .IsRequired();
        builder.Property(history => history.ChangeDate)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(history => history.Notes).HasMaxLength(2000);
        builder.HasIndex(history => new { history.IncidentId, history.ChangeDate });
        builder.HasOne(history => history.Incident)
            .WithMany()
            .HasForeignKey(history => history.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(history => history.ChangedByUser)
            .WithMany()
            .HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
