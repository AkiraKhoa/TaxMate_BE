using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface ISupplierRepository : IGenericRepository<Supplier>
{
    Task<IEnumerable<Supplier>> GetByBusinessAsync(Guid businessId);
    Task<int> GetCountByBusinessAsync(Guid businessId);
}
