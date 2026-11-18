using System.ComponentModel.DataAnnotations;

namespace IncidentManagementSystem.ViewModels;

public class UserCreateViewModel
{
    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "يجب اختيار رتبة صالحة.")]
    [StringLength(50)]
    public string Rank { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(12)]
    public string Password { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }

    [Required]
    public string RoleName { get; set; } = string.Empty;
}

public class UserUpdateViewModel
{
    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "يجب اختيار رتبة صالحة.")]
    [StringLength(50)]
    public string Rank { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }

    [Required]
    public string RoleName { get; set; } = string.Empty;
}

public class UserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Rank { get; set; }
    public string Email { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RoleName { get; set; }
}

public class UserPasswordResetViewModel
{
    [Required, DataType(DataType.Password), MinLength(12)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
