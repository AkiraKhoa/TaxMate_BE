using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IProductCategoryRepository : IGenericRepository<ProductCategory>
{
    Task<IEnumerable<ProductCategory>> GetByBusinessAsync(Guid businessId);
    Task<int> GetCountByBusinessAsync(Guid businessId);
}
