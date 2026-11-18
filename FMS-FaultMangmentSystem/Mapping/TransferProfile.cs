using AutoMapper;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Mapping;

public sealed class TransferProfile : Profile
{
    public TransferProfile()
    {
        CreateMap<TransferRequestCreateViewModel, IncidentTransferRequest>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.IncidentId, options => options.Ignore())
            .ForMember(destination => destination.FromDepartmentId, options => options.Ignore())
            .ForMember(destination => destination.ToDepartmentId, options => options.Ignore())
            .ForMember(destination => destination.RequestedByUserId, options => options.Ignore())
            .ForMember(destination => destination.RequestDate, options => options.Ignore())
            .ForMember(destination => destination.RequestStatus, options => options.Ignore())
            .ForMember(destination => destination.ReviewedBySupervisorId, options => options.Ignore())
            .ForMember(destination => destination.ReviewDate, options => options.Ignore())
            .ForMember(destination => destination.ReviewNotes, options => options.Ignore())
            .ForMember(destination => destination.Incident, options => options.Ignore())
            .ForMember(destination => destination.RequestedByUser, options => options.Ignore())
            .ForMember(destination => destination.ReviewedBySupervisor, options => options.Ignore());

        CreateMap<IncidentTransferRequest, TransferRequestViewModel>()
            .ForMember(destination => destination.IncidentNumber, options => options.Ignore())
            .ForMember(destination => destination.IncidentPriority, options => options.Ignore())
            .ForMember(destination => destination.RequestedByName, options => options.Ignore());
    }
}
