using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Interfaces.Documents;

public interface IQttDocumentGenerator
{
    Task<TaxDeclarationGeneratedFile> GenerateAsync(
        QttDocumentModel model,
        CancellationToken cancellationToken = default);
}
