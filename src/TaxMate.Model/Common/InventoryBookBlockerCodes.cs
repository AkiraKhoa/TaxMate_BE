namespace TaxMate.Model.Common;

public static class InventoryBookBlockerCodes
{
    public const string InvalidItem = "INVALID_ITEM";
    public const string ItemNotFound = "ITEM_NOT_FOUND";
    public const string ItemBusinessMismatch = "ITEM_BUSINESS_MISMATCH";
    public const string InvalidMovementType = "INVALID_MOVEMENT_TYPE";
    public const string InvalidReference = "INVALID_REFERENCE";
    public const string InvalidQuantity = "INVALID_QUANTITY";
    public const string InvalidValue = "INVALID_VALUE";
    public const string MissingInboundValue = "MISSING_INBOUND_VALUE";
    public const string MissingPriorOutboundValue = "MISSING_PRIOR_OUTBOUND_VALUE";
    public const string MissingValuationBase = "MISSING_VALUATION_BASE";
    public const string MissingOutboundValue = "MISSING_OUTBOUND_VALUE";
    public const string MissingUnit = "MISSING_UNIT";
    public const string NegativeInventory = "NEGATIVE_INVENTORY";
    public const string DuplicateSourceItem = "DUPLICATE_SOURCE_ITEM";
    public const string ConflictingFinalizedOutboundValue =
        "CONFLICTING_FINALIZED_OUTBOUND_VALUE";
    public const string MissingClosedBookQuarters =
        "MISSING_CLOSED_BOOK_QUARTERS";
}
