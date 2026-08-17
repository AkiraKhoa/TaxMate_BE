namespace TaxMate.Model.DTO;

public class CreateBusinessProfileRequest
{
    public Guid OwnerId { get; set; }
    public string BusinessName { get; set; } = null!;
    public string? ProvinceCode { get; set; }
    public string? WardCode { get; set; }
    public string? Address { get; set; }
    public Guid? MainCategoryId { get; set; }
    public bool PreferElectronicInvoice { get; set; }
    public bool IsStockTrackingEnabled { get; set; } = false;
}

public class UpdateBusinessProfileRequest
{
    public string BusinessName { get; set; } = null!;
    public string? ProvinceCode { get; set; }
    public string? WardCode { get; set; }
    public string? Address { get; set; }
    public Guid? MainCategoryId { get; set; }
    public bool PreferElectronicInvoice { get; set; }
    public bool? IsStockTrackingEnabled { get; set; }
}

public class ToggleStockTrackingRequest
{
    public bool IsStockTrackingEnabled { get; set; }
}

public class BusinessProfileResponse
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string BusinessName { get; set; } = null!;
    public string? ProvinceCode { get; set; }
    public string? WardCode { get; set; }
    public string? Address { get; set; }
    public Guid? MainCategoryId { get; set; }
    public string? MainCategoryName { get; set; }
    public bool PreferElectronicInvoice { get; set; }
    public bool IsStockTrackingEnabled { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
