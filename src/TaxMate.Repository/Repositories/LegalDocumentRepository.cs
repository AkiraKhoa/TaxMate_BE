using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class LegalDocumentRepository : GenericRepository<LegalDocument>, ILegalDocumentRepository
{
    public LegalDocumentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<bool> ExistsByDocumentCodeAsync(string documentCode)
    {
        return await _dbSet.AnyAsync(x =>
            x.DocumentCode == documentCode);
    }

    public async Task<bool> ExistsByFileHashAsync(
        string fileHash)
    {
        return await _dbSet.AnyAsync(x =>
            x.FileHash == fileHash);
    }
}
