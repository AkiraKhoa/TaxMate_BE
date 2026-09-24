using TaxMate.Model.Common;
using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IBusinessProfileService
{
    Task<BusinessProfileResponse> CreateAsync(
        Guid authenticatedOwnerId,
        CreateBusinessProfileRequest request);
    Task<BusinessProfileResponse> UpdateAsync(
        Guid authenticatedOwnerId,
        Guid id,
        UpdateBusinessProfileRequest request);
    Task<BusinessProfileResponse> ToggleStockTrackingAsync(
        Guid authenticatedOwnerId,
        Guid id,
        ToggleStockTrackingRequest request);
    Task DeactivateAsync(Guid authenticatedOwnerId, Guid id);
    Task<PagedResult<BusinessProfileResponse>> GetPagedAsync(
        Guid ownerId, int pageNumber, int pageSize, string? search);
    Task<BusinessProfileResponse> GetByIdAsync(Guid authenticatedOwnerId, Guid id);
}
