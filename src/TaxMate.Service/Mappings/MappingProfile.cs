using AutoMapper;
using TaxMate.Model.DTO;
using TaxMate.Model.DTO.LegalDocument;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<LegalDocument, LegalDocumentResponse>();

        CreateMap<ExpenseCategory, TaxMate.Model.DTO.ExpenseCategory.ExpenseCategoryDTO>();
        CreateMap<Expense, TaxMate.Model.DTO.Expense.ExpenseDTO>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.ExpenseCategory != null ? src.ExpenseCategory.CategoryName : string.Empty));
            
        CreateMap<IncomeCategory, TaxMate.Model.DTO.IncomeCategory.IncomeCategoryDTO>();
        CreateMap<Income, TaxMate.Model.DTO.Income.IncomeDTO>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.IncomeCategory != null ? src.IncomeCategory.CategoryName : string.Empty));
    }
}