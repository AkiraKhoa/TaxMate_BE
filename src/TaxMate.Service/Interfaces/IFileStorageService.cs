namespace TaxMate.Service.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}