using System.Security.Cryptography;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class LegalDocumentService : ILegalDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILegalDocumentRepository _legalDocuments;
    private readonly IFileStorageService _fileStorageService;

    public LegalDocumentService(
        IUnitOfWork unitOfWork,
        ILegalDocumentRepository legalDocuments,
        IFileStorageService fileStorageService)
    {
        _unitOfWork = unitOfWork;
        _legalDocuments = legalDocuments;
        _fileStorageService = fileStorageService;
    }
    
    public async Task<Guid> UploadAsync(
        UploadLegalDocumentRequest request)
    {
        var exists = await _legalDocuments
            .ExistsByDocumentCodeAsync(
                request.DocumentCode);

        if (exists)
        {
            throw new Exception(
                $"Document code '{request.DocumentCode}' already exists.");
        }
        
        await using var stream =
            request.File.OpenReadStream();

        // Calculate hash
        var fileHash =
            await CalculateHashAsync(stream);

        // Check for duplicates content
        var duplicatedFile =
            await _legalDocuments
                .ExistsByFileHashAsync(fileHash);

        if (duplicatedFile)
        {
            throw new Exception(
                "This document already exists.");
        }
        
        // Upload file
        var storagePath =
            await _fileStorageService.UploadAsync(
                stream,
                request.File.FileName,
                request.File.ContentType);

        var document = new LegalDocument
        {
            LegalDocumentId = Guid.NewGuid(),

            DocumentCode = request.DocumentCode,

            DocumentName = request.DocumentName,

            DocumentType = request.DocumentType,

            AuthorityLevel = request.AuthorityLevel,

            EffectiveDate = request.EffectiveDate,

            Status = "Active",

            SourceFileName = request.File.FileName,

            StoragePath = storagePath,

            FileSize = request.File.Length,

            FileHash = fileHash,

            IsIndexed = false,

            CreatedAt = DateTime.UtcNow
        };

        await _legalDocuments
            .AddAsync(document);

        await _unitOfWork.SaveChangesAsync();

        return document.LegalDocumentId;
    }
    
    // Helper method to calculate hash
    private static async Task<string> CalculateHashAsync(
        Stream stream)
    {
        using var sha256 = SHA256.Create();

        var hash =
            await sha256.ComputeHashAsync(stream);

        stream.Position = 0;

        return Convert.ToHexString(hash);
    }
}
