using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Title)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(notification => notification.Message)
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(notification => notification.CreatedAt)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(notification => notification.ReadAt).HasColumnType("datetime2(7)");
        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAt });
        builder.HasOne(notification => notification.User)
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(notification => notification.Incident)
            .WithMany(incident => incident.Notifications)
            .HasForeignKey(notification => notification.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}