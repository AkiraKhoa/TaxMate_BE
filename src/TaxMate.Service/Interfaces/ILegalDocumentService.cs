using TaxMate.Model.DTO;

namespace TaxMate.Service.Interfaces;

public interface ILegalDocumentService
{
    Task<Guid> UploadAsync(UploadLegalDocumentRequest request);
}