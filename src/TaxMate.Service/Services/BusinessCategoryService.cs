using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class BusinessCategoryService : IBusinessCategoryService
{
    private readonly IGenericRepository<BusinessCategory> _categories;

    public BusinessCategoryService(IGenericRepository<BusinessCategory> categories)
    {
        _categories = categories;
    }

    public async Task<List<BusinessCategoryResponse>> GetAllAsync()
    {
        var items = await _categories.GetAllAsync();
        return items
            .OrderBy(x => x.Code)
            .Select(x => new BusinessCategoryResponse
            {
                Id = x.BusinessCategoryId,
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                VatRate = x.VatRate,
                PitRate = x.PitRate
            })
            .ToList();
    }
}
