using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class IncidentTransferRequestConfiguration : IEntityTypeConfiguration<IncidentTransferRequest>
{
    public void Configure(EntityTypeBuilder<IncidentTransferRequest> builder)
    {
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Reason)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(request => request.RequestDate)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(request => request.RequestStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)")
            .HasDefaultValue(TransferRequestStatus.Pending);
        builder.Property(request => request.ReviewDate).HasColumnType("datetime2(7)");
        builder.Property(request => request.ReviewNotes).HasMaxLength(2000);
        builder.HasIndex(request => new { request.IncidentId, request.RequestDate });
        builder.HasIndex(request => new { request.RequestStatus, request.RequestDate });
        builder.HasOne(request => request.FromDepartment)
            .WithMany()
            .HasForeignKey(request => request.FromDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(request => request.ToDepartment)
            .WithMany()
            .HasForeignKey(request => request.ToDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(request => request.Incident)
            .WithMany()
            .HasForeignKey(request => request.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(request => request.RequestedByUser)
            .WithMany()
            .HasForeignKey(request => request.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(request => request.ReviewedBySupervisor)
            .WithMany()
            .HasForeignKey(request => request.ReviewedBySupervisorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
