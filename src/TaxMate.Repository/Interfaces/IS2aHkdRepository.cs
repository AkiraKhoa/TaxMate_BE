using TaxMate.Model.DTO;

namespace TaxMate.Repository.Interfaces;

public interface IS2aHkdRepository
{
    Task<List<S2aHkdProductAggregate>> GetProductAggregatesAsync(
        Guid businessId,
        DateTime startDate,
        DateTime endDate);
}
