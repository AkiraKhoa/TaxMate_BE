using TaxMate.Model.Common;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IBusinessProfileService
{
    Task<BusinessProfileResponse> CreateAsync(CreateBusinessProfileRequest request);
    Task<BusinessProfileResponse> UpdateAsync(Guid id, UpdateBusinessProfileRequest request);
    Task DeactivateAsync(Guid id);
    Task<PagedResult<BusinessProfileResponse>> GetPagedAsync(
        Guid ownerId, int pageNumber, int pageSize, string? search);
    Task<BusinessProfileResponse> GetByIdAsync(Guid id);
}
