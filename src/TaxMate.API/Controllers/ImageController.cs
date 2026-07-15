using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly IImageStorageService _imageStorageService;

    public ImageController(IImageStorageService imageStorageService)
    {
        _imageStorageService = imageStorageService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ApiResponse<string> { Success = false, Message = "File is empty or null" });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new ApiResponse<string> { Success = false, Message = "File is not a supported image type" });
        }

        using var stream = file.OpenReadStream();
        var imageUrl = await _imageStorageService.UploadImageAsync(stream, file.FileName, file.ContentType);

        return Ok(ApiResponse<string>.Ok(imageUrl, "Image uploaded successfully", HttpContext.TraceIdentifier));
    }
}
