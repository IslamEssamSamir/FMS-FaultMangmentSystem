using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IncidentManagementSystem.Data.Configurations;

public class ShiftDefinitionConfiguration : IEntityTypeConfiguration<ShiftDefinition>
{
    public void Configure(EntityTypeBuilder<ShiftDefinition> builder)
    {
        builder.HasKey(definition => definition.Id);
        builder.Property(definition => definition.Name)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(definition => definition.StartTime).HasColumnType("time(0)");
        builder.Property(definition => definition.EndTime).HasColumnType("time(0)");
        builder.Property(definition => definition.IsActive).HasDefaultValue(true);
        builder.HasIndex(definition => definition.Name)
            .IsUnique();
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ShiftDefinitions_TimeRange",
            "([IsOvernight] = 1) OR ([IsOvernight] = 0 AND [StartTime] < [EndTime])"));
    }
}
