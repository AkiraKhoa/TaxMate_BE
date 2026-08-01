using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IIngredientPurchaseRepository : IGenericRepository<IngredientPurchase>
{
    Task<(List<IngredientPurchase> Items, int TotalCount)> GetPagedByBusinessAsync(
        Guid businessId, int pageNumber, int pageSize, string? search);

    Task<IngredientPurchase?> GetByIdWithDetailsAsync(Guid id);
}
