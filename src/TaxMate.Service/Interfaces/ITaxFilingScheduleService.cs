using TaxMate.Model.DTO.TaxFiling;

namespace TaxMate.Service.Interfaces;

public interface ITaxFilingScheduleService
{
    Task<IReadOnlyList<TaxFilingTaskSummaryResponse>> GetTasksAsync(
        Guid userId,
        Guid businessId,
        int year,
        CancellationToken cancellationToken = default);

    Task<TaxFilingTaskSummaryResponse> OpenTaskAsync(
        Guid userId,
        Guid businessId,
        string taskId,
        CancellationToken cancellationToken = default);
}
