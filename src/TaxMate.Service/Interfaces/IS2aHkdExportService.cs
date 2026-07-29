using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IS2aHkdExportService
{
    Task<S2aHkdDocumentModel> BuildDocumentModelAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        int quarter);

    Task<byte[]> ExportDocxAsync(
        Guid ownerId,
        Guid businessId,
        int year,
        int quarter);
}
