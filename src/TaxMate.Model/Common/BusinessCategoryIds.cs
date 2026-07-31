namespace TaxMate.Model.Common;

/// <summary>
/// Stable GUIDs for seeded official tax rate groups (TT 152 / NĐ 68).
/// </summary>
public static class BusinessCategoryIds
{
    public static readonly Guid DistGoods = new("a0000001-0000-4000-8000-000000000001");
    public static readonly Guid ProdTransport = new("a0000001-0000-4000-8000-000000000002");
    public static readonly Guid ServiceConstruct = new("a0000001-0000-4000-8000-000000000003");
    public static readonly Guid AssetInsurance = new("a0000001-0000-4000-8000-000000000004");
    public static readonly Guid Other = new("a0000001-0000-4000-8000-000000000005");
}
