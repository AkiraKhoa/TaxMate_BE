using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IBusinessCategoryService
{
    Task<List<BusinessCategoryResponse>> GetAllAsync();
}
