using IncidentManagementSystem.Data;
using IncidentManagementSystem.Models.Entities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace IncidentManagementSystem.Tests;

public class IncidentDepartmentAssignmentModelSnapshotTests
{
    [Fact]
    public void SnapshotIncludesIncidentDepartmentCollectionNavigations()
    {
        var snapshotType = typeof(ApplicationDbContext).Assembly.GetType(
            "IncidentManagementSystem.Migrations.ApplicationDbContextModelSnapshot",
            throwOnError: true)!;
        var snapshot = (ModelSnapshot)Activator.CreateInstance(snapshotType, nonPublic: true)!;

        var incidentEntity = snapshot.Model.FindEntityType(typeof(Incident));

        Assert.NotNull(incidentEntity?.FindNavigation(nameof(Incident.DepartmentActions)));
        Assert.NotNull(incidentEntity?.FindNavigation(nameof(Incident.DepartmentAssignments)));
    }
}
