using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IIngredientRepository : IGenericRepository<Ingredient>
{
    Task<Ingredient?> GetByIdAndBusinessAsync(Guid id, Guid businessId);

    Task<(List<Ingredient> Items, int TotalCount)> GetPagedByBusinessAsync(
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search);
}
