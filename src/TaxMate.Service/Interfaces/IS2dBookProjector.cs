using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;

namespace TaxMate.Service.Interfaces;

public interface IS2dBookProjector
{
    S2dBook ProjectQuarter(
        Guid businessId,
        IReadOnlyCollection<InventoryMovement> movementsBeforePeriodEnd,
        int year,
        int quarter,
        bool requireFinalValues = false);
}
