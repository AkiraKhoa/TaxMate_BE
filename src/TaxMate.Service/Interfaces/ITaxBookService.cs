using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Interfaces;

public interface ITaxBookService
{
    Task<TaxDeclarationGeneratedFile> ExportS1aAsync(
        Guid userId,
        Guid businessId,
        int year,
        int? month,
        CancellationToken cancellationToken = default);
}
