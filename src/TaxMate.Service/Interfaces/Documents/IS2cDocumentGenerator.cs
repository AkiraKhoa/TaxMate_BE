using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Interfaces.Documents;

public interface IS2cDocumentGenerator
{
    Task<TaxDeclarationGeneratedFile> GenerateAsync(
        S2cDocumentModel model,
        CancellationToken cancellationToken = default);
}
