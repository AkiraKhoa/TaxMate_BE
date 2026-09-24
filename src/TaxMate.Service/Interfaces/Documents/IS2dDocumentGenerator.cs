using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Interfaces.Documents;

public interface IS2dDocumentGenerator
{
    Task<TaxDeclarationGeneratedFile> GenerateAsync(
        S2dDocumentModel model,
        CancellationToken cancellationToken = default);
}
