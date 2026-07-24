using TaxMate.Model.Documents.Tax;

namespace TaxMate.Service.Interfaces.Documents;

public interface ITaxDeclarationDocumentGenerator
{
    Task<TaxDeclarationGeneratedFile> GenerateAsync(
        Form01Cnkd2026Model model,
        CancellationToken cancellationToken = default);
}