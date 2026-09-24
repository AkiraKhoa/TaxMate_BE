using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

internal sealed class InventoryMovementCoordinatorValidator
    : IInventoryMovementCoordinatorValidator
{
    private readonly IExpenseRepository _expenses;
    private readonly ITransactionRepository _transactions;

    public InventoryMovementCoordinatorValidator(
        IExpenseRepository expenses,
        ITransactionRepository transactions)
    {
        _expenses = expenses;
        _transactions = transactions;
    }

    public async Task EnsureValidReferenceTargetAsync(
        InventoryMovementReferenceTarget target,
        CancellationToken cancellationToken = default)
    {
        if (target.MovementType == InventoryMovementTypes.PurchaseIn)
        {
            var expense = await _expenses.GetByIdAsync(target.ReferenceId);
            if (expense is null || expense.BusinessId != target.BusinessId)
            {
                throw new NotFoundException(
                    "Không tìm thấy chứng từ chi mua hàng thuộc cửa hàng này.");
            }

            return;
        }

        if (target.MovementType == InventoryMovementTypes.OrderOut)
        {
            var transaction = await _transactions.GetByIdAsync(target.ReferenceId);
            if (transaction is null || transaction.BusinessId != target.BusinessId)
            {
                throw new NotFoundException(
                    "Không tìm thấy đơn bán hàng thuộc cửa hàng này.");
            }

            return;
        }

        throw new BadRequestException(
            "Chỉ PurchaseIn và OrderOut được phép tham chiếu chứng từ nguồn.");
    }
}
