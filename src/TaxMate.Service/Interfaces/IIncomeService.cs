using TaxMate.Model.Common;
using TaxMate.Model.DTO.Income;

namespace TaxMate.Service.Interfaces;

public interface IIncomeService
{
    Task<IncomeDTO> CreateAsync(Guid ownerId, Guid businessId, CreateIncomeRequest request);
    Task<IncomeDTO> UpdateAsync(Guid ownerId, Guid id, UpdateIncomeRequest request);
    Task DeleteAsync(Guid ownerId, Guid id);
    Task<IncomeDTO> GetByIdAsync(Guid ownerId, Guid id);
    Task<PagedResult<IncomeDTO>> GetPagedAsync(
        Guid ownerId,
        Guid businessId,
        int pageNumber,
        int pageSize,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? categoryId,
        string? paymentMethod);
    Task<IncomeSummaryDTO> GetMonthlySummaryAsync(Guid ownerId, Guid businessId, int year, int month);
}
