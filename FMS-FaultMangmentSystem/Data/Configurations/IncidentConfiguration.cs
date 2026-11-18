using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.HasKey(incident => incident.Id);
        builder.Property(incident => incident.IncidentNumber)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(incident => incident.Title)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(incident => incident.Description)
            .HasMaxLength(4000)
            .IsRequired();
        builder.Property(incident => incident.Priority)
            .HasConversion<int>()
            .HasColumnType("int")
            .IsRequired();
        builder.Property(incident => incident.ReportingEntity)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(incident => incident.ReporterName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(incident => incident.ReporterPhone)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(incident => incident.NotificationMethod)
            .HasConversion<int>()
            .HasColumnType("int")
            .IsRequired();
        builder.Property(incident => incident.OtherNotificationMethodDescription)
            .HasMaxLength(500);
        builder.Property(incident => incident.OfficialLetterAttachmentKey)
            .HasMaxLength(100);
        builder.Property(incident => incident.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)")
            .HasDefaultValue(IncidentStatus.Incoming);
        builder.Property(incident => incident.FinalSolutionDescription)
            .HasMaxLength(4000);
        builder.Property(incident => incident.ResolutionSteps)
            .HasMaxLength(8000);
        builder.Property(incident => incident.InitialDutyOfficerFullNameSnapshot)
            .HasMaxLength(200);
        builder.Property(incident => incident.InitialDutyOfficerRankSnapshot)
            .HasMaxLength(50);
        builder.Property(incident => incident.CreatedAt)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(incident => incident.UpdatedAt).HasColumnType("datetime2(7)");
        builder.Property(incident => incident.ResolvedAt).HasColumnType("datetime2(7)");
        builder.Property(incident => incident.ClosedAt).HasColumnType("datetime2(7)");
        builder.HasIndex(incident => incident.IncidentNumber)
            .IsUnique();
        builder.HasIndex(incident => new { incident.CurrentDepartmentId, incident.Status });
        builder.HasIndex(incident => incident.CreatedAt);
        builder.HasOne(incident => incident.ReportedByUser)
            .WithMany()
            .HasForeignKey(incident => incident.ReportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(incident => incident.ReportingDepartment)
            .WithMany()
            .HasForeignKey(incident => incident.ReportingDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(incident => incident.InitialReceivingDepartment)
            .WithMany()
            .HasForeignKey(incident => incident.InitialReceivingDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(incident => incident.CurrentDepartment)
            .WithMany()
            .HasForeignKey(incident => incident.CurrentDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(incident => incident.CurrentResponsibleUser)
            .WithMany()
            .HasForeignKey(incident => incident.CurrentResponsibleUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(incident => incident.ResolvedByUser)
            .WithMany()
            .HasForeignKey(incident => incident.ResolvedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
