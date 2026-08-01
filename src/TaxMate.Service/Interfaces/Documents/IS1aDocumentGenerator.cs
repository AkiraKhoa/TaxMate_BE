using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Interfaces.Documents;

public interface IS1aDocumentGenerator
{
    Task<TaxDeclarationGeneratedFile> GenerateAsync(
        S1aDocumentModel model,
        CancellationToken cancellationToken = default);
}
