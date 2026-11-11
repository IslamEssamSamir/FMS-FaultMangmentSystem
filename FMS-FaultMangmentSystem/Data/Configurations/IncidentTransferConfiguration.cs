using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class IncidentTransferConfiguration : IEntityTypeConfiguration<IncidentTransfer>
{
    public void Configure(EntityTypeBuilder<IncidentTransfer> builder)
    {
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.TransferReason)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(transfer => transfer.TransferDate)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(transfer => transfer.DutyOfficerFullNameSnapshot).HasMaxLength(200);
        builder.Property(transfer => transfer.DutyOfficerRankSnapshot).HasMaxLength(50);
        builder.HasIndex(transfer => new { transfer.IncidentId, transfer.TransferDate });
        builder.HasIndex(transfer => new { transfer.FromDepartmentId, transfer.ToDepartmentId });
        builder.HasOne(transfer => transfer.Incident)
            .WithMany()
            .HasForeignKey(transfer => transfer.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.FromDepartment)
            .WithMany()
            .HasForeignKey(transfer => transfer.FromDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.ToDepartment)
            .WithMany()
            .HasForeignKey(transfer => transfer.ToDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.TransferredBySupervisor)
            .WithMany()
            .HasForeignKey(transfer => transfer.TransferredBySupervisorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
