using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class IncidentReviewConfiguration : IEntityTypeConfiguration<IncidentReview>
{
    public void Configure(EntityTypeBuilder<IncidentReview> builder)
    {
        builder.HasKey(review => review.Id);
        builder.Property(review => review.ReviewDecision)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnType("nvarchar(20)")
            .IsRequired();
        builder.Property(review => review.ReviewNotes).HasMaxLength(2000);
        builder.Property(review => review.ReviewDate)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(review => new { review.IncidentId, review.ReviewDate });
        builder.HasOne(review => review.Incident)
            .WithMany()
            .HasForeignKey(review => review.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(review => review.Supervisor)
            .WithMany()
            .HasForeignKey(review => review.SupervisorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
