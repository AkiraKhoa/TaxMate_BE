using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Interfaces.Documents;

public interface ITknDeclarationDocumentGenerator
{
    Task<TaxDeclarationGeneratedFile> GenerateAsync(
        Form01TknCnkd2026Snapshot snapshot,
        CancellationToken cancellationToken = default);
}
