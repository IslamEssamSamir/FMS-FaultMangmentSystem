using AutoMapper;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Mapping;

public sealed class IncidentProfile : Profile
{
    public IncidentProfile()
    {
        CreateMap<IncidentCreateViewModel, Incident>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.IncidentNumber, options => options.Ignore())
            .ForMember(destination => destination.Status, options => options.Ignore())
            .ForMember(destination => destination.ReportedByUserId, options => options.Ignore())
            .ForMember(destination => destination.ReportingDepartmentId, options => options.Ignore())
            .ForMember(destination => destination.InitialReceivingDepartmentId, options => options.Ignore())
            .ForMember(destination => destination.CurrentDepartmentId, options => options.Ignore())
            .ForMember(destination => destination.CurrentResponsibleUserId, options => options.Ignore())
            .ForMember(destination => destination.ResolvedByUserId, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.Ignore())
            .ForMember(destination => destination.UpdatedAt, options => options.Ignore())
            .ForMember(destination => destination.ResolvedAt, options => options.Ignore())
            .ForMember(destination => destination.ClosedAt, options => options.Ignore())
            .ForMember(destination => destination.ReportedByUser, options => options.Ignore())
            .ForMember(destination => destination.ReportingDepartment, options => options.Ignore())
            .ForMember(destination => destination.InitialReceivingDepartment, options => options.Ignore())
            .ForMember(destination => destination.CurrentDepartment, options => options.Ignore())
            .ForMember(destination => destination.CurrentResponsibleUser, options => options.Ignore())
            .ForMember(destination => destination.ResolvedByUser, options => options.Ignore());

        CreateMap<Incident, IncidentListItemViewModel>()
            .ForMember(destination => destination.CurrentDepartmentName, options => options.Ignore());

        CreateMap<Incident, IncidentDetailsViewModel>()
            .IncludeBase<Incident, IncidentListItemViewModel>()
            .ForMember(destination => destination.ReportingDepartmentName, options => options.Ignore())
            .ForMember(destination => destination.InitialReceivingDepartmentName, options => options.Ignore())
            .ForMember(destination => destination.CurrentResponsibleUserName, options => options.Ignore())
            .ForMember(destination => destination.ResolvedByUserName, options => options.Ignore())
            .ForMember(destination => destination.Actions, options => options.Ignore());

        CreateMap<IncidentActionCreateViewModel, IncidentAction>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.IncidentId, options => options.Ignore())
            .ForMember(destination => destination.UserId, options => options.Ignore())
            .ForMember(destination => destination.ActionDate, options => options.Ignore())
            .ForMember(destination => destination.Incident, options => options.Ignore())
            .ForMember(destination => destination.User, options => options.Ignore());

        CreateMap<IncidentAction, IncidentActionViewModel>()
            .ForMember(destination => destination.UserFullName, options => options.Ignore());
    }
}
