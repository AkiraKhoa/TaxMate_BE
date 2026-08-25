using TaxMate.Model.Common;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

internal sealed class MoneyMovementService : IMoneyMovementService
{
    private readonly IMoneyMovementRepository _moneyMovements;

    public MoneyMovementService(IMoneyMovementRepository moneyMovements)
    {
        _moneyMovements = moneyMovements;
    }

    public async Task<MoneyMovementWriteResult> SyncAsync(
        MoneyMovementWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var (paymentMethod, movementDate) = ValidateRequest(request);
        await EnsureOwnerAsync(request.OwnerId, request.BusinessId, cancellationToken);

        var account = await _moneyMovements.GetAccountForWriteAsync(
            request.PaymentAccountId,
            cancellationToken);
        ValidateAccount(account, request.BusinessId, paymentMethod);

        var documentNumber = request.DocumentNumber.Trim();
        var description = request.Description.Trim();
        var existing = await _moneyMovements.GetBySourceForWriteAsync(
            request.MovementType,
            request.ReferenceId,
            cancellationToken);

        if (existing is null)
        {
            var movement = new MoneyMovement
            {
                MoneyMovementId = Guid.NewGuid(),
                PaymentAccountId = request.PaymentAccountId,
                MovementType = request.MovementType,
                Amount = request.Amount,
                MovementDate = movementDate,
                DocumentNumber = documentNumber,
                Description = description,
                ReferenceId = request.ReferenceId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _moneyMovements.AddAsync(movement, cancellationToken);
            return new MoneyMovementWriteResult
            {
                MoneyMovementId = movement.MoneyMovementId,
                Outcome = MoneyMovementWriteOutcome.Created
            };
        }

        EnsureMovementContext(existing, request.OwnerId, request.BusinessId);

        var isUnchanged =
            existing.PaymentAccountId == request.PaymentAccountId &&
            existing.Amount == request.Amount &&
            existing.MovementDate == movementDate &&
            existing.DocumentNumber == documentNumber &&
            existing.Description == description;

        if (isUnchanged)
        {
            return new MoneyMovementWriteResult
            {
                MoneyMovementId = existing.MoneyMovementId,
                Outcome = MoneyMovementWriteOutcome.Unchanged
            };
        }

        existing.PaymentAccountId = request.PaymentAccountId;
        existing.Amount = request.Amount;
        existing.MovementDate = movementDate;
        existing.DocumentNumber = documentNumber;
        existing.Description = description;
        existing.UpdatedAt = DateTime.UtcNow;

        return new MoneyMovementWriteResult
        {
            MoneyMovementId = existing.MoneyMovementId,
            Outcome = MoneyMovementWriteOutcome.Updated
        };
    }

    public async Task<bool> DeleteAsync(
        Guid ownerId,
        Guid businessId,
        string movementType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
    {
        ValidateContext(ownerId, businessId);
        ValidateMovementIdentity(movementType, referenceId);
        await EnsureOwnerAsync(ownerId, businessId, cancellationToken);

        var existing = await _moneyMovements.GetBySourceForWriteAsync(
            movementType,
            referenceId,
            cancellationToken);
        if (existing is null)
        {
            return false;
        }

        EnsureMovementContext(existing, ownerId, businessId);
        _moneyMovements.Remove(existing);
        return true;
    }

    private async Task EnsureOwnerAsync(
        Guid ownerId,
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var actualOwnerId = await _moneyMovements.GetBusinessOwnerIdAsync(
            businessId,
            cancellationToken);
        if (actualOwnerId is null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        if (actualOwnerId != ownerId)
        {
            throw new ForbiddenException();
        }
    }

    private static (string PaymentMethod, DateTime MovementDate) ValidateRequest(
        MoneyMovementWriteRequest request)
    {
        ValidateContext(request.OwnerId, request.BusinessId);
        ValidateMovementIdentity(request.MovementType, request.ReferenceId);

        if (request.PaymentAccountId == Guid.Empty)
        {
            throw new BadRequestException("Payment account is required.");
        }

        var paymentMethod = NormalizePaymentMethod(request.PaymentMethod);

        if (request.Amount <= 0)
        {
            throw new BadRequestException("Money movement amount must be greater than zero.");
        }

        if (request.MovementDate == default)
        {
            throw new BadRequestException("Money movement date is required.");
        }

        var movementDate = BangkokBusinessTime.NormalizeNaiveUtc(
            request.MovementDate);

        ValidateSnapshot(request.DocumentNumber, 100, "Document number");
        ValidateSnapshot(request.Description, 1000, "Description");
        return (paymentMethod, movementDate);
    }

    private static void ValidateContext(Guid ownerId, Guid businessId)
    {
        if (ownerId == Guid.Empty)
        {
            throw new BadRequestException("Owner is required.");
        }

        if (businessId == Guid.Empty)
        {
            throw new BadRequestException("Business is required.");
        }
    }

    private static void ValidateMovementIdentity(string movementType, Guid referenceId)
    {
        if (!MoneyMovementTypes.All.Contains(movementType))
        {
            throw new BadRequestException("Unsupported money movement type.");
        }

        if (referenceId == Guid.Empty)
        {
            throw new BadRequestException("Money movement source reference is required.");
        }
    }

    private static void ValidateSnapshot(string? value, int maximumLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException($"{fieldName} is required.");
        }

        if (value.Trim().Length > maximumLength)
        {
            throw new BadRequestException($"{fieldName} must not exceed {maximumLength} characters.");
        }
    }

    private static string NormalizePaymentMethod(string? paymentMethod)
    {
        if (string.Equals(paymentMethod?.Trim(), PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase))
        {
            return PaymentMethods.Cash;
        }

        if (string.Equals(paymentMethod?.Trim(), PaymentMethods.Transfer, StringComparison.OrdinalIgnoreCase))
        {
            return PaymentMethods.Transfer;
        }

        throw new BadRequestException("Payment method must be Cash or Transfer.");
    }

    private static void ValidateAccount(
        PaymentAccount? account,
        Guid businessId,
        string paymentMethod)
    {
        if (account is null || account.BusinessId != businessId)
        {
            throw new NotFoundException("Payment account not found.");
        }

        if (!account.IsActive)
        {
            throw new BadRequestException("Inactive payment accounts cannot receive new movements.");
        }

        var expectedAccountType = paymentMethod == PaymentMethods.Cash
            ? PaymentAccountTypes.Cash
            : PaymentAccountTypes.Bank;
        if (account.AccountType != expectedAccountType)
        {
            throw new BadRequestException(
                paymentMethod == PaymentMethods.Cash
                    ? "Cash payments must use the system cash account."
                    : "Transfer payments must use an active bank account.");
        }
    }

    private static void EnsureMovementContext(
        MoneyMovement movement,
        Guid ownerId,
        Guid businessId)
    {
        if (movement.PaymentAccount.BusinessId != businessId)
        {
            throw new ForbiddenException();
        }

        if (movement.PaymentAccount.Business.OwnerId != ownerId)
        {
            throw new ForbiddenException();
        }
    }
}
