using IncidentManagementSystem.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.FullName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(user => user.Rank)
            .HasMaxLength(50);
        builder.Property(user => user.IsActive)
            .HasDefaultValue(true);
        builder.Property(user => user.CreatedAt)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(user => user.DepartmentId);
        builder.HasOne(user => user.Department)
            .WithMany()
            .HasForeignKey(user => user.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
