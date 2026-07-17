using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface ISupplierService
{
    Task<SupplierResponse> CreateAsync(Guid ownerId, Guid businessId, CreateSupplierRequest request);
    Task<SupplierResponse> UpdateAsync(Guid ownerId, Guid id, UpdateSupplierRequest request);
    Task DeleteAsync(Guid ownerId, Guid id);
    Task<IEnumerable<SupplierResponse>> GetByBusinessAsync(Guid ownerId, Guid businessId);
    Task<SupplierResponse> GetByIdAsync(Guid ownerId, Guid id);
}
