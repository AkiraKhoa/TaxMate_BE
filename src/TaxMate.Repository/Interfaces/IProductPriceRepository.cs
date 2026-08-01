using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IProductPriceRepository : IGenericRepository<ProductPrice>
{
    Task<List<ProductPrice>> GetByProductIdAsync(Guid productId);

    Task<ProductPrice?> FindByProductIdAndApplyDateAsync(Guid productId, DateTime applyDate);

    Task<bool> ExistsDuplicateApplyDateAsync(
        Guid productId,
        DateTime applyDate,
        Guid? excludeId = null);
}
