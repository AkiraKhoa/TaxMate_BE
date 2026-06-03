using System.Security.Cryptography;
using AutoMapper;
using TaxMate.Model.DTO.LegalDocument;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class LegalDocumentService : ILegalDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMapper _mapper;

    public LegalDocumentService(
        IUnitOfWork unitOfWork, 
        IFileStorageService fileStorageService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _mapper = mapper;
    }
    
    public async Task<Guid> UploadAsync(
        UploadLegalDocumentRequest request)
    {
        var exists = await _unitOfWork.LegalDocuments
            .ExistsByDocumentCodeAsync(
                request.DocumentCode);

        if (exists)
        {
            throw new ConflictException(
                $"Document code '{request.DocumentCode}' already exists.");
        }
        
        await using var stream =
            request.File.OpenReadStream();

        // Calculate hash
        var fileHash =
            await CalculateHashAsync(stream);

        // Check for duplicates content
        var duplicatedFile =
            await _unitOfWork.LegalDocuments
                .ExistsByFileHashAsync(fileHash);

        if (duplicatedFile)
        {
            throw new ConflictException(
                $"File '{request.DocumentName}' content already exists.");
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

        await _unitOfWork.LegalDocuments
            .AddAsync(document);

        await _unitOfWork.SaveChangesAsync();

        return document.LegalDocumentId;
    }

    public async Task<List<LegalDocumentResponse>> GetAllAsync()
    {
        var documents =
            await _unitOfWork.LegalDocuments
                .GetAllAsync();

        return _mapper.Map<
            List<LegalDocumentResponse>>(
            documents);
    }

    public async Task<LegalDocumentResponse> GetByIdAsync(Guid id)
    {
        var document =
            await _unitOfWork.LegalDocuments
                .GetByIdAsync(id);

        if (document == null)
        {
            throw new NotFoundException(
                "Legal document not found.");
        }

        return _mapper.Map<LegalDocumentResponse>(
            document);
    }
    
    public async Task DeactivateAsync(Guid id)
    {
        var document =
            await _unitOfWork.LegalDocuments
                .GetByIdAsync(id);

        if (document == null)
        {
            throw new NotFoundException(
                "Legal document not found.");
        }

        if (document.Status == "Inactive")
        {
            throw new ConflictException(
                "Document already inactive.");
        }

        document.Status = "Inactive";

        _unitOfWork.LegalDocuments
            .Update(document);

        await _unitOfWork.SaveChangesAsync();
    }
    
    public async Task ActivateAsync(Guid id)
    {
        var document =
            await _unitOfWork.LegalDocuments
                .GetByIdAsync(id);

        if (document == null)
        {
            throw new NotFoundException(
                "Legal document not found.");
        }

        if (document.Status == "Active")
        {
            throw new ConflictException(
                "Document already active.");
        }

        document.Status = "Active";

        _unitOfWork.LegalDocuments
            .Update(document);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<List<LegalDocumentResponse>> GetActiveAsync()
    {
        var documents =
            await _unitOfWork.LegalDocuments
                .GetActiveAsync();

        return _mapper.Map<
            List<LegalDocumentResponse>>(
            documents);   
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