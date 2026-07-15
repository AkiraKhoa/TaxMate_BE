namespace TaxMate.Service.Interfaces;

public interface IImageStorageService
{
    Task<string> UploadImageAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
