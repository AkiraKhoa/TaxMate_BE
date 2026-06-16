using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface IIngredientRepository : IGenericRepository<Ingredient>
{
    Task<(List<Ingredient> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? search);
}
