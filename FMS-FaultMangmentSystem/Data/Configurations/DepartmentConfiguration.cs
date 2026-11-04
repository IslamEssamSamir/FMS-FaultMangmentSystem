using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.HasKey(department => department.Id);
        builder.Property(department => department.Name)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(department => department.Description)
            .HasMaxLength(1000);
        builder.Property(department => department.IsActive)
            .HasDefaultValue(true);
        builder.Property(department => department.CreatedAt)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(department => department.Name)
            .IsUnique();
        builder.HasIndex(department => new { department.IsActive, department.IsIncidentReceivingDepartment });
    }
}
