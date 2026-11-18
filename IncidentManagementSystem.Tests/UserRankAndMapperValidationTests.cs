using AutoMapper;
using IncidentManagementSystem.Authorization;
using IncidentManagementSystem.Mapping;
using IncidentManagementSystem.Models.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IncidentManagementSystem.Tests;

public class UserRankAndMapperValidationTests
{
    [Fact]
    public void UserRank_ContainsExactlyTheRequiredArabicOrder()
    {
        Assert.Equal(
            new[]
            {
                "لواء",
                "عميد",
                "عقيد",
                "مقدم",
                "رائد",
                "نقيب",
                "ملازم أول",
                "ملازم",
                "أمين شرطة",
                "عسكري",
                "موظف"
            },
            UserRank.All);
    }

    [Theory]
    [InlineData("موظف", RolePermissions.CivilianEmployee)]
    [InlineData("لواء", RolePermissions.SystemAdmin)]
    [InlineData("ملازم", RolePermissions.SystemSupervisor)]
    public void CivilianRuleAndRoleSelection_AreResolvedAsExpected(string rank, string roleName)
    {
        var submittedRole = rank == "موظف" ? RolePermissions.SystemAdmin : roleName;
        var effectiveRole = UserRoleSelection.GetEffectiveRoleName(rank, submittedRole);

        if (rank == "موظف")
        {
            Assert.Equal(RolePermissions.CivilianEmployee, effectiveRole);
        }
        else
        {
            Assert.Equal(submittedRole, effectiveRole);
        }
    }

    [Fact]
    public void AutoMapper_Configuration_IsValid()
    {
        var mapperConfiguration = new MapperConfiguration(configuration =>
        {
            configuration.AddProfile<UserProfile>();
        }, NullLoggerFactory.Instance);

        mapperConfiguration.AssertConfigurationIsValid();
    }
}
