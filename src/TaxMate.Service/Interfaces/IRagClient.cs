using TaxMate.Model.DTO.Rag;

namespace TaxMate.Service.Interfaces;

public interface IRagClient
{
    Task<RagAskResponse> AskAsync(
        RagAskRequest request,
        CancellationToken cancellationToken = default);
}