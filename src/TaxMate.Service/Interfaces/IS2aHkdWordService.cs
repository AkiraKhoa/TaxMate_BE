using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IS2aHkdWordService
{
    Task<byte[]> GenerateDocxAsync(IReadOnlyList<S2aHkdDocumentModel> models);
}
