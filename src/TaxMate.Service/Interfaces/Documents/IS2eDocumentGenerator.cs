using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Interfaces.Documents;

public interface IS2eDocumentGenerator
{
    Task<TaxDeclarationGeneratedFile> GenerateAsync(
        S2eDocumentModel model,
        CancellationToken cancellationToken = default);
}
