using TaxMate.Model.Common;
using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    Task<(List<Product> Items, int TotalCount)> GetPagedByBusinessAsync(
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search,
        string? status,
        ProductCategory? category);

    Task<Product?> GetByIdWithPricesAsync(Guid id);
}
