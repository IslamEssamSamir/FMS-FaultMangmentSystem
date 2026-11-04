using AutoMapper;
using IncidentManagementSystem.Models.Entities;
using IncidentManagementSystem.ViewModels;

namespace IncidentManagementSystem.Mapping;

public sealed class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<IncidentReviewCreateViewModel, IncidentReview>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.ReviewDecision, options => options.Ignore())
            .ForMember(destination => destination.IncidentId, options => options.Ignore())
            .ForMember(destination => destination.SupervisorId, options => options.Ignore())
            .ForMember(destination => destination.ReviewDate, options => options.Ignore())
            .ForMember(destination => destination.Incident, options => options.Ignore())
            .ForMember(destination => destination.Supervisor, options => options.Ignore());

        CreateMap<IncidentReview, IncidentReviewViewModel>()
            .ForMember(destination => destination.IncidentNumber, options => options.Ignore())
            .ForMember(destination => destination.SupervisorName, options => options.Ignore());
    }
}
