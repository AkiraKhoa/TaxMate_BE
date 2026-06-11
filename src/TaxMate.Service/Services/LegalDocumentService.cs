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
    private readonly ILegalDocumentRepository _legalDocuments;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMapper _mapper;

    public LegalDocumentService(
        IUnitOfWork unitOfWork,
        ILegalDocumentRepository legalDocuments,
        IFileStorageService fileStorageService,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _legalDocuments = legalDocuments;
        _fileStorageService = fileStorageService;
        _mapper = mapper;
    }
    
    public async Task<Guid> UploadAsync(
        UploadLegalDocumentRequest request)
    {
        var exists = await _legalDocuments
            .ExistsByDocumentCodeAsync(
                request.DocumentCode);

        if (exists)
        {
            throw new ConflictException(
                $"Document code '{request.DocumentCode}' already exists.");
        }
        
        await using var stream =
            request.File.OpenReadStream();

        var fileHash =
            await CalculateHashAsync(stream);

        var duplicatedFile =
            await _legalDocuments
                .ExistsByFileHashAsync(fileHash);

        if (duplicatedFile)
        {
            throw new ConflictException(
                $"File '{request.DocumentName}' content already exists.");
        }
        
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

    public async Task<List<LegalDocumentResponse>> GetAllAsync()
    {
        var documents =
            await _legalDocuments.GetAllAsync();

        return _mapper.Map<List<LegalDocumentResponse>>(documents);
    }

    public async Task<LegalDocumentResponse> GetByIdAsync(Guid id)
    {
        var document =
            await _legalDocuments.GetByIdAsync(id);

        if (document == null)
        {
            throw new NotFoundException(
                "Legal document not found.");
        }

        return _mapper.Map<LegalDocumentResponse>(document);
    }
    
    public async Task DeactivateAsync(Guid id)
    {
        var document =
            await _legalDocuments.GetByIdAsync(id);

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

        _legalDocuments.Update(document);

        await _unitOfWork.SaveChangesAsync();
    }
    
    public async Task ActivateAsync(Guid id)
    {
        var document =
            await _legalDocuments.GetByIdAsync(id);

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

        _legalDocuments.Update(document);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<List<LegalDocumentResponse>> GetActiveAsync()
    {
        var documents =
            await _legalDocuments.GetActiveAsync();

        return _mapper.Map<List<LegalDocumentResponse>>(documents);
    }

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
