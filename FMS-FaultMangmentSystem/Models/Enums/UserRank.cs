using IncidentManagementSystem.Authorization;

namespace IncidentManagementSystem.Models.Enums;

public static class UserRank
{
    public const string General = "لواء";
    public const string MajorGeneral = "عميد";
    public const string Colonel = "عقيد";
    public const string LieutenantColonel = "مقدم";
    public const string Captain = "رائد";
    public const string FirstLieutenant = "نقيب";
    public const string Lieutenant = "ملازم أول";
    public const string SecondLieutenant = "ملازم";
    public const string PoliceConstable = "أمين شرطة";
    public const string Military = "عسكري";
    public const string Civilian = "موظف";

    public static readonly IReadOnlyList<string> All =
    [
        General,
        MajorGeneral,
        Colonel,
        LieutenantColonel,
        Captain,
        FirstLieutenant,
        Lieutenant,
        SecondLieutenant,
        PoliceConstable,
        Military,
        Civilian
    ];

    public static bool IsAllowed(string? value)
    {
        var normalized = Normalize(value);
        return !string.IsNullOrWhiteSpace(normalized) && All.Contains(normalized, StringComparer.Ordinal);
    }

    public static bool IsCivilian(string? value) => string.Equals(Normalize(value), Civilian, StringComparison.Ordinal);

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class UserRoleSelection
{
    public static readonly IReadOnlyList<KeyValuePair<string, string>> Options =
    [
        new(RolePermissions.SystemAdmin, "ضابط مدير النظام"),
        new(RolePermissions.SystemSupervisor, "ضابط مشرف النظام"),
        new(RolePermissions.User, "ضابط مستخدم"),
        new(RolePermissions.CivilianEmployee, "موظف مدني")
    ];

    public static string GetDisplayName(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return string.Empty;
        }

        var match = Options.FirstOrDefault(item => string.Equals(item.Key, roleName.Trim(), StringComparison.Ordinal));
        return match.Value ?? roleName.Trim();
    }

    public static string GetEffectiveRoleName(string? rank, string? submittedRoleName)
    {
        if (UserRank.IsCivilian(rank))
        {
            return RolePermissions.CivilianEmployee;
        }

        var normalizedRole = submittedRoleName?.Trim();
        return string.IsNullOrWhiteSpace(normalizedRole) ? string.Empty : normalizedRole;
    }
}
