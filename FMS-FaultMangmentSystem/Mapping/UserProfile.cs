using AutoMapper;
using IncidentManagementSystem.Models.Identity;
using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Mapping;

public sealed class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<ApplicationUser, UserViewModel>()
            .ForMember(destination => destination.DepartmentId, options => options.MapFrom(source => source.DepartmentId))
            .ForMember(destination => destination.DepartmentName, options => options.Ignore())
            .ForMember(destination => destination.RoleName, options => options.Ignore());

        CreateMap<UserCreateViewModel, ApplicationUser>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.UserName, options => options.MapFrom(source => source.Email))
            .ForMember(destination => destination.Email, options => options.MapFrom(source => source.Email))
            .ForMember(destination => destination.FullName, options => options.MapFrom(source => source.FullName))
            .ForMember(destination => destination.DepartmentId, options => options.Ignore())
            .ForMember(destination => destination.Department, options => options.Ignore())
            .ForMember(destination => destination.IsActive, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.Ignore())
            .ForMember(destination => destination.PasswordHash, options => options.Ignore())
            .ForMember(destination => destination.SecurityStamp, options => options.Ignore())
            .ForMember(destination => destination.ConcurrencyStamp, options => options.Ignore())
            .ForMember(destination => destination.NormalizedEmail, options => options.Ignore())
            .ForMember(destination => destination.NormalizedUserName, options => options.Ignore())
            .ForMember(destination => destination.EmailConfirmed, options => options.Ignore())
            .ForMember(destination => destination.PhoneNumber, options => options.Ignore())
            .ForMember(destination => destination.PhoneNumberConfirmed, options => options.Ignore())
            .ForMember(destination => destination.TwoFactorEnabled, options => options.Ignore())
            .ForMember(destination => destination.LockoutEnd, options => options.Ignore())
            .ForMember(destination => destination.LockoutEnabled, options => options.Ignore())
            .ForMember(destination => destination.AccessFailedCount, options => options.Ignore());

        CreateMap<UserUpdateViewModel, ApplicationUser>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.UserName, options => options.Ignore())
            .ForMember(destination => destination.Email, options => options.Ignore())
            .ForMember(destination => destination.NormalizedEmail, options => options.Ignore())
            .ForMember(destination => destination.NormalizedUserName, options => options.Ignore())
            .ForMember(destination => destination.DepartmentId, options => options.Ignore())
            .ForMember(destination => destination.Department, options => options.Ignore())
            .ForMember(destination => destination.IsActive, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.Ignore())
            .ForMember(destination => destination.PasswordHash, options => options.Ignore())
            .ForMember(destination => destination.SecurityStamp, options => options.Ignore())
            .ForMember(destination => destination.ConcurrencyStamp, options => options.Ignore())
            .ForMember(destination => destination.EmailConfirmed, options => options.Ignore())
            .ForMember(destination => destination.PhoneNumber, options => options.Ignore())
            .ForMember(destination => destination.PhoneNumberConfirmed, options => options.Ignore())
            .ForMember(destination => destination.TwoFactorEnabled, options => options.Ignore())
            .ForMember(destination => destination.LockoutEnd, options => options.Ignore())
            .ForMember(destination => destination.LockoutEnabled, options => options.Ignore())
            .ForMember(destination => destination.AccessFailedCount, options => options.Ignore());
    }
}
