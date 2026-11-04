using System.ComponentModel.DataAnnotations;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.ViewModels;
using Xunit;

namespace IncidentManagementSystem.Tests;

public class ManagementViewModelTests
{
    [Fact]
    public void UserPasswordReset_RequiresMatchingStrongPasswordFields()
    {
        var model = new UserPasswordResetViewModel
        {
            NewPassword = "short",
            ConfirmPassword = "different"
        };
        var errors = Validate(model);
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.NewPassword)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.ConfirmPassword)));

        model.NewPassword = "Abcd1234!Secure";
        model.ConfirmPassword = model.NewPassword;
        Assert.Empty(Validate(model));
    }

    [Fact]
    public void DepartmentCreate_RequiresNameAndRejectsOversizedValues()
    {
        var model = new DepartmentCreateViewModel
        {
            Name = string.Empty,
            Description = new string('x', 1001)
        };

        var errors = Validate(model);
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.Name)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.Description)));
    }

    [Fact]
    public void UserRoleChoices_ContainExactlyTheFourPredefinedRoles()
    {
        Assert.Equal(4, RolePermissions.All.Count);
        Assert.Equal(
            new[] { RolePermissions.CivilianEmployee, RolePermissions.SystemAdmin, RolePermissions.SystemSupervisor, RolePermissions.User }
                .OrderBy(role => role, StringComparer.Ordinal),
            RolePermissions.All.Keys.OrderBy(role => role, StringComparer.Ordinal));
    }

    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
