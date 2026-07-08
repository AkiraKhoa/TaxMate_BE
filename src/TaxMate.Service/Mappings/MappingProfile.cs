using AutoMapper;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.LegalDocument;
using TaxMate.Model.DTO.PlanFeature;
using TaxMate.Model.DTO.SubscriptionPlan;
using TaxMate.Model.DTO.UserDevice;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // LegalDocument mappings
        CreateMap<LegalDocument, LegalDocumentResponse>();

        CreateMap<ExpenseCategory, TaxMate.Model.DTO.ExpenseCategory.ExpenseCategoryDTO>();
        CreateMap<Expense, TaxMate.Model.DTO.Expense.ExpenseDTO>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.ExpenseCategory != null ? src.ExpenseCategory.CategoryName : string.Empty));
            
        CreateMap<IncomeCategory, TaxMate.Model.DTO.IncomeCategory.IncomeCategoryDTO>();
        CreateMap<Income, TaxMate.Model.DTO.Income.IncomeDTO>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.IncomeCategory != null ? src.IncomeCategory.CategoryName : string.Empty));
        
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

        CreateMap<RegisterDeviceRequest, UserDevice>();
    }
}