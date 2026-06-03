using TaxMate.Model.Entities;

namespace TaxMate.Repository.Interfaces;

public interface ILegalDocumentRepository : IGenericRepository<LegalDocument>
{
    Task<bool> ExistsByDocumentCodeAsync(string documentCode);
    Task<bool> ExistsByFileHashAsync(string fileHash);
    Task<List<LegalDocument>> GetActiveAsync();
}