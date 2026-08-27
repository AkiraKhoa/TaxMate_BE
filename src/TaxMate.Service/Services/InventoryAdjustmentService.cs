using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

internal sealed class InventoryAdjustmentService : IInventoryAdjustmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryControlRepository _controls;
    private readonly IInventoryMovementService _movements;
    private readonly ITaxPeriodMutationGuard _mutationGuard;

    public InventoryAdjustmentService(
        IUnitOfWork unitOfWork,
        IInventoryControlRepository controls,
        IInventoryMovementService movements,
        ITaxPeriodMutationGuard mutationGuard)
    {
        _unitOfWork = unitOfWork;
        _controls = controls;
        _movements = movements;
        _mutationGuard = mutationGuard;
    }

    public async Task<InventoryControlResultResponse> ReconcileAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        ReconcileInventoryRequest request,
        bool enableStockTracking,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _mutationGuard.EnsureCanCreateAsync(
                authenticatedOwnerId,
                businessId,
                request.OccurredAt,
                cancellationToken);
            var business = await RequireOwnedBusinessAsync(
                authenticatedOwnerId,
                businessId,
                cancellationToken);
            var products = await _controls.GetActiveProductsAsync(
                businessId,
                tracking: true,
                cancellationToken);
            var ingredients = await _controls.GetActiveIngredientsAsync(
                businessId,
                tracking: true,
                cancellationToken);
            var existing = await _controls.GetMovementsAsync(
                businessId,
                tracking: true,
                cancellationToken);
            var lines = request.Lines ?? [];
            var submittedKeys = lines
                .Select(x => InventoryControlRules.GetKey(x.ProductId, x.IngredientId))
                .ToArray();
            InventoryControlRules.EnsureExactActiveItemSet(
                InventoryControlRules.ActiveKeys(products, ingredients),
                submittedKeys);

            var currentQuantities = InventoryControlRules.CalculateQuantities(existing);
            var productsById = products.ToDictionary(x => x.Id);
            var ingredientsById = ingredients.ToDictionary(x => x.Id);
            var adjustmentInCount = 0;
            var adjustmentOutCount = 0;

            foreach (var line in lines)
            {
                if (line.ActualQuantity < 0m)
                {
                    throw new BadRequestException(
                        "Số lượng kiểm kê thực tế không được âm.");
                }

                if (line.AdjustmentInTotalValue < 0m)
                {
                    throw new BadRequestException(
                        "Giá trị điều chỉnh tăng không được âm.");
                }

                var key = InventoryControlRules.GetKey(
                    line.ProductId,
                    line.IngredientId);
                var current = currentQuantities.GetValueOrDefault(key);
                var delta = line.ActualQuantity - current;
                if (delta > 0m)
                {
                    if (!line.AdjustmentInTotalValue.HasValue)
                    {
                        throw new BadRequestException(
                            "Phần tồn kiểm kê thừa phải có giá trị điều chỉnh tăng.");
                    }

                    await StageAdjustmentAsync(
                        businessId,
                        key,
                        InventoryMovementTypes.AdjustmentIn,
                        delta,
                        line.AdjustmentInTotalValue,
                        request,
                        cancellationToken);
                    adjustmentInCount++;
                }
                else if (delta < 0m)
                {
                    if (line.AdjustmentInTotalValue.HasValue)
                    {
                        throw new BadRequestException(
                            "Điều chỉnh giảm do hệ thống định giá khi chốt quý; không nhập giá trị tại đây.");
                    }

                    await StageAdjustmentAsync(
                        businessId,
                        key,
                        InventoryMovementTypes.AdjustmentOut,
                        decimal.Abs(delta),
                        totalValue: null,
                        request,
                        cancellationToken);
                    adjustmentOutCount++;
                }
                else if (line.AdjustmentInTotalValue.HasValue)
                {
                    throw new BadRequestException(
                        "Không có chênh lệch nên không được nhập giá trị điều chỉnh.");
                }

                if (key.ProductId.HasValue)
                {
                    productsById[key.ProductId.Value].StockQuantity = line.ActualQuantity;
                }
                else
                {
                    ingredientsById[key.IngredientId!.Value].StockQuantity = line.ActualQuantity;
                }
            }

            if (enableStockTracking)
            {
                business.IsStockTrackingEnabled = true;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return new InventoryControlResultResponse
            {
                BusinessId = businessId,
                IsStockTrackingEnabled = business.IsStockTrackingEnabled,
                AdjustmentInCount = adjustmentInCount,
                AdjustmentOutCount = adjustmentOutCount,
                Items = InventoryControlRules.MapItems(products, ingredients)
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private Task<InventoryMovement> StageAdjustmentAsync(
        Guid businessId,
        InventoryItemKey key,
        string movementType,
        decimal quantity,
        decimal? totalValue,
        ReconcileInventoryRequest request,
        CancellationToken cancellationToken)
    {
        return _movements.StageAdjustmentAsync(new StageInventoryAdjustmentCommand
        {
            BusinessId = businessId,
            MovementType = movementType,
            ProductId = key.ProductId,
            IngredientId = key.IngredientId,
            Quantity = quantity,
            TotalValue = totalValue,
            OccurredAt = request.OccurredAt,
            DocumentNumber = request.DocumentNumber,
            Description = request.Description
        }, cancellationToken);
    }

    private async Task<BusinessProfile> RequireOwnedBusinessAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var business = await _controls.GetBusinessAsync(businessId, cancellationToken);
        if (business is null || !business.IsActive)
        {
            throw new NotFoundException("Business profile not found.");
        }

        if (business.OwnerId != authenticatedOwnerId)
        {
            throw new ForbiddenException();
        }

        return business;
    }
}
