using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;

namespace TaxMate.Repository.Repositories;

public class LegalDocumentRepository : GenericRepository<LegalDocument>, ILegalDocumentRepository
{
    private readonly AppDbContext _context;

    public LegalDocumentRepository(AppDbContext context) : base(context)
    {
        _context = context;
    }


    public async Task<bool> ExistsByDocumentCodeAsync(string documentCode)
    {
        return await _context.LegalDocuments
            .AnyAsync(x =>
                x.DocumentCode == documentCode);    }

    public async Task<bool> ExistsByFileHashAsync(
        string fileHash)
    {
        return await _dbSet.AnyAsync(x =>
            x.FileHash == fileHash);
    }
}