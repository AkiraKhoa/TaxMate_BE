using TaxMate.Model.DTO.LegalDocument;

namespace TaxMate.Service.Interfaces;

public interface ILegalDocumentService
{
    Task<Guid> UploadAsync(UploadLegalDocumentRequest request);
    Task<List<LegalDocumentResponse>> GetAllAsync();
    Task<LegalDocumentResponse> GetByIdAsync(Guid id);
    Task DeactivateAsync(Guid id);
    Task ActivateAsync(Guid id);
    Task<List<LegalDocumentResponse>> GetActiveAsync();
}