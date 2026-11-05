using AutoMapper;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Mapping;

public sealed class DepartmentProfile : Profile
{
    public DepartmentProfile()
    {
        CreateMap<Department, DepartmentViewModel>();
        CreateMap<DepartmentCreateViewModel, Department>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.IsActive, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.Ignore());
        CreateMap<DepartmentUpdateViewModel, Department>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.Ignore());
    }
}




























































































