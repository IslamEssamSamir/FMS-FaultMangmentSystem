using AutoMapper;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Mapping;

public sealed class AuditProfile : Profile
{
    public AuditProfile()
    {
        CreateMap<AuditLog, AuditLogViewModel>()
            .ForMember(destination => destination.UserName, options => options.Ignore());
    }
}
