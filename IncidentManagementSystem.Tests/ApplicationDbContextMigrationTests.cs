using IncidentManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IncidentManagementSystem.Tests;

public class ApplicationDbContextMigrationTests
{
    [Fact]
    public void ModelSnapshotMatchesCurrentModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=FMSModelSnapshotValidation;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var dbContext = new ApplicationDbContext(options);

        Assert.False(dbContext.Database.HasPendingModelChanges());
    }
}
