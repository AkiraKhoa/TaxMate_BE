using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Interfaces.Documents;

public interface IS2bDocumentGenerator
{
    Task<TaxDeclarationGeneratedFile> GenerateAsync(
        S2bDocumentModel model,
        CancellationToken cancellationToken = default);
}
