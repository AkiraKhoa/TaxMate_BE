using TaxMate.Model.DTO.Inventory;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

internal sealed class InventoryInitializationService : IInventoryInitializationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryControlRepository _controls;
    private readonly IInventoryMovementService _movements;
    private readonly ITaxPeriodMutationGuard _mutationGuard;

    public InventoryInitializationService(
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

    public async Task<InventoryInitializationPreviewResponse> GetPreviewAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        var business = await RequireOwnedBusinessAsync(
            authenticatedOwnerId,
            businessId,
            cancellationToken);
        var products = await _controls.GetActiveProductsAsync(
            businessId,
            tracking: false,
            cancellationToken);
        var ingredients = await _controls.GetActiveIngredientsAsync(
            businessId,
            tracking: false,
            cancellationToken);
        var movements = await _controls.GetMovementsAsync(
            businessId,
            tracking: false,
            cancellationToken);

        return new InventoryInitializationPreviewResponse
        {
            BusinessId = businessId,
            IsInitialized = business.InventoryInitializedAt.HasValue || movements.Count > 0,
            IsStockTrackingEnabled = business.IsStockTrackingEnabled,
            Items = InventoryControlRules.MapItems(products, ingredients)
        };
    }

    public async Task<InventoryControlResultResponse> InitializeAsync(
        Guid authenticatedOwnerId,
        Guid businessId,
        InitializeInventoryRequest request,
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
            var existing = await _controls.GetMovementsAsync(
                businessId,
                tracking: true,
                cancellationToken);
            if (business.InventoryInitializedAt.HasValue || existing.Count > 0)
            {
                throw new ConflictException(
                    "Sổ tồn kho đã được khởi tạo; không thể tạo tồn đầu lần nữa.");
            }

            var products = await _controls.GetActiveProductsAsync(
                businessId,
                tracking: true,
                cancellationToken);
            var ingredients = await _controls.GetActiveIngredientsAsync(
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

            var productsById = products.ToDictionary(x => x.Id);
            var ingredientsById = ingredients.ToDictionary(x => x.Id);
            var openingLines = new List<InventoryMovementLineInput>();
            foreach (var line in lines)
            {
                if (line.Quantity < 0m)
                {
                    throw new BadRequestException(
                        "Số lượng tồn ban đầu không được âm.");
                }

                if (line.TotalValue < 0m)
                {
                    throw new BadRequestException(
                        "Giá trị tồn ban đầu không được âm.");
                }

                var key = InventoryControlRules.GetKey(
                    line.ProductId,
                    line.IngredientId);
                if (line.Quantity == 0m)
                {
                    if (line.TotalValue is not null and not 0m)
                    {
                        throw new BadRequestException(
                            "Mặt hàng có số lượng bằng 0 không được có giá trị tồn đầu.");
                    }
                }
                else
                {
                    if (!line.TotalValue.HasValue)
                    {
                        throw new BadRequestException(
                            "Mặt hàng có tồn ban đầu phải có cả số lượng và giá trị.");
                    }

                    openingLines.Add(new InventoryMovementLineInput
                    {
                        ProductId = key.ProductId,
                        IngredientId = key.IngredientId,
                        Quantity = line.Quantity,
                        TotalValue = line.TotalValue
                    });
                }

                if (key.ProductId.HasValue)
                {
                    var product = productsById[key.ProductId.Value];
                    product.StockQuantity = line.Quantity;
                    if (line.Quantity > 0m)
                    {
                        product.CostPrice = RoundUnit(line.TotalValue!.Value / line.Quantity);
                    }
                }
                else
                {
                    var ingredient = ingredientsById[key.IngredientId!.Value];
                    ingredient.StockQuantity = line.Quantity;
                    if (line.Quantity > 0m)
                    {
                        ingredient.EstimatedPrice = RoundUnit(
                            line.TotalValue!.Value / line.Quantity);
                    }
                }
            }

            var staged = await _movements.StageOpeningBalancesAsync(new()
            {
                BusinessId = businessId,
                OccurredAt = request.OccurredAt,
                DocumentNumber = request.DocumentNumber,
                Description = request.Description,
                Lines = openingLines
            }, cancellationToken);

            business.InventoryInitializedAt = DateTime.SpecifyKind(
                DateTime.UtcNow,
                DateTimeKind.Unspecified);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return new InventoryControlResultResponse
            {
                BusinessId = businessId,
                IsStockTrackingEnabled = business.IsStockTrackingEnabled,
                OpeningBalanceCount = staged.Count,
                Items = InventoryControlRules.MapItems(products, ingredients)
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<TaxMate.Model.Entities.BusinessProfile> RequireOwnedBusinessAsync(
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

    private static decimal RoundUnit(decimal value) =>
        decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}
