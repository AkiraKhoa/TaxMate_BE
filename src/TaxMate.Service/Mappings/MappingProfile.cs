using AutoMapper;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.LegalDocument;
using TaxMate.Model.DTO.PlanFeature;
using TaxMate.Model.DTO.SubscriptionPlan;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // LegalDocument mappings
        CreateMap<LegalDocument, LegalDocumentResponse>();
        
        // SubscriptionPlan & PlanFeature mappings
        CreateMap<CreatePlanFeatureRequest, PlanFeature>();

        CreateMap<SubscriptionPlan, SubscriptionPlanResponse>();

        CreateMap<PlanFeature, PlanFeatureResponse>();    
        
        CreateMap<CreateSubscriptionPlanRequest, SubscriptionPlan>()
            .ForMember(
                dest => dest.PlanFeatures,
                opt => opt.MapFrom(src => src.Features));
        
        CreateMap<UpdateSubscriptionPlanRequest, SubscriptionPlan>()
            .ForMember(
                dest => dest.PlanFeatures,
                opt => opt.Ignore())
            .ForMember(
                dest => dest.Id,
                opt => opt.Ignore())
            .ForMember(
                dest => dest.IsActive,
                opt => opt.Ignore());

        CreateMap<UpdatePlanFeatureRequest, PlanFeature>()
            .ForMember(
                dest => dest.Id,
                opt => opt.MapFrom(src => src.Id ?? Guid.NewGuid()));
    }
}