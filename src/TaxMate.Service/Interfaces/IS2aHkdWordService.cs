using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface IS2aHkdWordService
{
    Task<byte[]> GenerateDocxAsync(S2aHkdDocumentModel model);
}
