using AutoMapper;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Mapping;

public sealed class ShiftProfile : Profile
{
    public ShiftProfile()
    {
        CreateMap<ShiftDefinition, ShiftDefinitionViewModel>();
        CreateMap<ShiftDefinitionCreateViewModel, ShiftDefinition>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.IsActive, options => options.Ignore());
        CreateMap<ShiftDefinitionUpdateViewModel, ShiftDefinition>()
            .ForMember(destination => destination.Id, options => options.Ignore());

        CreateMap<ShiftAssignmentCreateViewModel, ShiftAssignment>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.WeeklyShiftScheduleId, options => options.Ignore())
            .ForMember(destination => destination.DepartmentId, options => options.Ignore())
            .ForMember(destination => destination.UserId, options => options.Ignore())
            .ForMember(destination => destination.ShiftDefinitionId, options => options.Ignore())
            .ForMember(destination => destination.WeeklyShiftSchedule, options => options.Ignore())
            .ForMember(destination => destination.Department, options => options.Ignore())
            .ForMember(destination => destination.User, options => options.Ignore())
            .ForMember(destination => destination.ShiftDefinition, options => options.Ignore());

        CreateMap<ShiftAssignment, ShiftAssignmentViewModel>()
            .ForMember(destination => destination.Id, options => options.MapFrom(source => source.Id))
            .ForMember(destination => destination.DepartmentId, options => options.MapFrom(source => source.DepartmentId))
            .ForMember(destination => destination.UserId, options => options.MapFrom(source => source.UserId))
            .ForMember(destination => destination.ShiftDefinitionId, options => options.MapFrom(source => source.ShiftDefinitionId))
            .ForMember(destination => destination.DepartmentName, options => options.Ignore())
            .ForMember(destination => destination.UserFullName, options => options.Ignore())
            .ForMember(destination => destination.ShiftDefinitionName, options => options.Ignore())
            .ForMember(destination => destination.StartTime, options => options.Ignore())
            .ForMember(destination => destination.EndTime, options => options.Ignore())
            .ForMember(destination => destination.IsOvernight, options => options.Ignore());
    }
}
