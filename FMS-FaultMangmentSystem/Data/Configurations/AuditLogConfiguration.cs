using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedOnAdd();
        builder.Property(log => log.Action)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(log => log.EntityName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(log => log.EntityId).HasMaxLength(450);
        builder.Property(log => log.Details).HasColumnType("nvarchar(max)");
        builder.Property(log => log.CreatedAt)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(log => log.CreatedAt);
        builder.HasIndex(log => new { log.UserId, log.CreatedAt });
        builder.HasOne(log => log.User)
            .WithMany()
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
