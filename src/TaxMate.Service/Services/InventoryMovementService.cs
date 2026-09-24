using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

internal sealed class InventoryMovementService : IInventoryMovementService
{
    private readonly IInventoryMovementRepository _movements;
    private readonly IInventoryMovementCoordinatorValidator _coordinatorValidator;

    public InventoryMovementService(
        IInventoryMovementRepository movements,
        IInventoryMovementCoordinatorValidator coordinatorValidator)
    {
        _movements = movements;
        _coordinatorValidator = coordinatorValidator;
    }

    public async Task<IReadOnlyList<InventoryMovement>> StageReplaceSourceAsync(
        ReplaceInventorySourceMovementsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureBusiness(command.BusinessId);
        EnsureSourceType(command.MovementType);
        if (command.ReferenceId == Guid.Empty)
        {
            throw new BadRequestException("ReferenceId của phát sinh kho không hợp lệ.");
        }

        var header = NormalizeHeader(
            command.OccurredAt,
            command.DocumentNumber,
            command.Description);
        await _coordinatorValidator.EnsureValidReferenceTargetAsync(
            new InventoryMovementReferenceTarget(
                command.BusinessId,
                command.MovementType,
                command.ReferenceId),
            cancellationToken);
        var aggregated = AggregateLines(command.Lines, command.MovementType);
        await EnsureItemOwnershipAsync(
            command.BusinessId,
            aggregated.Keys,
            cancellationToken);

        var existing = await _movements.GetBySourceForUpdateAsync(
            command.BusinessId,
            command.MovementType,
            command.ReferenceId,
            cancellationToken);

        var duplicate = existing
            .GroupBy(ToKey)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ConflictException(
                "Nguồn nghiệp vụ đang có nhiều phát sinh kho cho cùng một mặt hàng.");
        }

        var existingByItem = existing.ToDictionary(ToKey);
        var staged = new List<InventoryMovement>(aggregated.Count);

        foreach (var (key, line) in aggregated)
        {
            if (existingByItem.Remove(key, out var movement))
            {
                movement.Quantity = line.Quantity;
                movement.TotalValue = line.TotalValue;
                movement.OccurredAt = header.OccurredAt;
                movement.DocumentNumber = header.DocumentNumber;
                movement.Description = header.Description;
                _movements.Update(movement);
            }
            else
            {
                movement = NewMovement(
                    command.BusinessId,
                    key,
                    command.MovementType,
                    line.Quantity,
                    line.TotalValue,
                    header,
                    command.ReferenceId);
                await _movements.AddAsync(movement);
            }

            staged.Add(movement);
        }

        if (existingByItem.Count > 0)
        {
            _movements.RemoveRange(existingByItem.Values);
        }

        return staged;
    }

    public async Task StageRemoveSourceAsync(
        Guid businessId,
        string movementType,
        Guid referenceId,
        CancellationToken cancellationToken = default)
    {
        EnsureBusiness(businessId);
        EnsureSourceType(movementType);
        if (referenceId == Guid.Empty)
        {
            throw new BadRequestException("ReferenceId của phát sinh kho không hợp lệ.");
        }

        await _coordinatorValidator.EnsureValidReferenceTargetAsync(
            new InventoryMovementReferenceTarget(
                businessId,
                movementType,
                referenceId),
            cancellationToken);

        var existing = await _movements.GetBySourceForUpdateAsync(
            businessId,
            movementType,
            referenceId,
            cancellationToken);
        _movements.RemoveRange(existing);
    }

    public async Task<IReadOnlyList<InventoryMovement>> StageOpeningBalancesAsync(
        StageInventoryOpeningBalancesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureBusiness(command.BusinessId);
        var header = NormalizeHeader(
            command.OccurredAt,
            command.DocumentNumber,
            command.Description);
        var aggregated = AggregateLines(
            command.Lines,
            InventoryMovementTypes.OpeningBalance);
        await EnsureItemOwnershipAsync(
            command.BusinessId,
            aggregated.Keys,
            cancellationToken);

        var staged = new List<InventoryMovement>(aggregated.Count);
        foreach (var (key, line) in aggregated)
        {
            var movement = await _movements.GetOpeningForUpdateAsync(
                command.BusinessId,
                key.ProductId,
                key.IngredientId,
                cancellationToken);

            if (movement is null)
            {
                movement = NewMovement(
                    command.BusinessId,
                    key,
                    InventoryMovementTypes.OpeningBalance,
                    line.Quantity,
                    line.TotalValue,
                    header,
                    referenceId: null);
                await _movements.AddAsync(movement);
            }
            else
            {
                movement.Quantity = line.Quantity;
                movement.TotalValue = line.TotalValue;
                movement.OccurredAt = header.OccurredAt;
                movement.DocumentNumber = header.DocumentNumber;
                movement.Description = header.Description;
                _movements.Update(movement);
            }

            staged.Add(movement);
        }

        return staged;
    }

    public async Task<InventoryMovement> StageAdjustmentAsync(
        StageInventoryAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureBusiness(command.BusinessId);
        if (command.MovementType is not
            (InventoryMovementTypes.AdjustmentIn or InventoryMovementTypes.AdjustmentOut))
        {
            throw new BadRequestException(
                "Kiểm kho chỉ có thể tạo điều chỉnh tăng hoặc điều chỉnh giảm.");
        }

        var line = new InventoryMovementLineInput
        {
            ProductId = command.ProductId,
            IngredientId = command.IngredientId,
            Quantity = command.Quantity,
            TotalValue = command.TotalValue
        };
        var aggregated = AggregateLines([line], command.MovementType);
        var pair = aggregated.Single();
        await EnsureItemOwnershipAsync(
            command.BusinessId,
            [pair.Key],
            cancellationToken);
        var header = NormalizeHeader(
            command.OccurredAt,
            command.DocumentNumber,
            command.Description);

        var movement = NewMovement(
            command.BusinessId,
            pair.Key,
            command.MovementType,
            pair.Value.Quantity,
            pair.Value.TotalValue,
            header,
            referenceId: null);
        await _movements.AddAsync(movement);
        return movement;
    }

    private async Task EnsureItemOwnershipAsync(
        Guid businessId,
        IReadOnlyCollection<InventoryItemKey> keys,
        CancellationToken cancellationToken)
    {
        var productIds = keys
            .Where(x => x.ProductId.HasValue)
            .Select(x => x.ProductId!.Value)
            .Distinct()
            .ToArray();
        var ingredientIds = keys
            .Where(x => x.IngredientId.HasValue)
            .Select(x => x.IngredientId!.Value)
            .Distinct()
            .ToArray();

        var products = await _movements.GetProductsIncludingDeletedAsync(
            productIds,
            cancellationToken);
        var ingredients = await _movements.GetIngredientsIncludingDeletedAsync(
            ingredientIds,
            cancellationToken);
        var productsById = products.ToDictionary(x => x.Id);
        var ingredientsById = ingredients.ToDictionary(x => x.Id);

        foreach (var productId in productIds)
        {
            if (!productsById.TryGetValue(productId, out var product))
            {
                throw new NotFoundException($"Không tìm thấy hàng hóa '{productId}'.");
            }

            if (product.BusinessId != businessId)
            {
                throw new BadRequestException(
                    "Hàng hóa không thuộc cửa hàng của phát sinh kho.");
            }
        }

        foreach (var ingredientId in ingredientIds)
        {
            if (!ingredientsById.TryGetValue(ingredientId, out var ingredient))
            {
                throw new NotFoundException($"Không tìm thấy nguyên liệu '{ingredientId}'.");
            }

            if (ingredient.BusinessId != businessId)
            {
                throw new BadRequestException(
                    "Nguyên liệu không thuộc cửa hàng của phát sinh kho.");
            }
        }
    }

    private static Dictionary<InventoryItemKey, AggregatedLine> AggregateLines(
        IReadOnlyList<InventoryMovementLineInput>? lines,
        string movementType)
    {
        lines ??= [];
        var result = new Dictionary<InventoryItemKey, AggregatedLine>();

        foreach (var line in lines)
        {
            if (line is null)
            {
                throw new BadRequestException("Dòng phát sinh kho không hợp lệ.");
            }

            var key = ValidateItem(line.ProductId, line.IngredientId);
            if (line.Quantity <= 0)
            {
                throw new BadRequestException("Số lượng phát sinh kho phải lớn hơn 0.");
            }

            ValidateValue(movementType, line.TotalValue);
            if (!result.TryGetValue(key, out var aggregate))
            {
                aggregate = new AggregatedLine();
                result.Add(key, aggregate);
            }

            aggregate.Quantity += line.Quantity;
            if (line.TotalValue.HasValue)
            {
                aggregate.TotalValue = (aggregate.TotalValue ?? 0m) + line.TotalValue.Value;
            }
        }

        return result;
    }

    private static void ValidateValue(string movementType, decimal? value)
    {
        if (value < 0)
        {
            throw new BadRequestException("Giá trị phát sinh kho không được âm.");
        }

        if (movementType is InventoryMovementTypes.OpeningBalance or InventoryMovementTypes.PurchaseIn)
        {
            if (!value.HasValue)
            {
                throw new BadRequestException(
                    "Tồn đầu và nhập mua phải có giá trị.");
            }

            return;
        }

        if (movementType is InventoryMovementTypes.OrderOut or InventoryMovementTypes.AdjustmentOut)
        {
            if (value.HasValue)
            {
                throw new BadRequestException(
                    "Giá trị xuất kho do hệ thống tính khi chốt kỳ.");
            }

            return;
        }

        if (movementType != InventoryMovementTypes.AdjustmentIn)
        {
            throw new BadRequestException("Loại phát sinh kho không hợp lệ.");
        }
    }

    private static InventoryItemKey ValidateItem(
        Guid? productId,
        Guid? ingredientId)
    {
        if (productId.HasValue == ingredientId.HasValue ||
            productId == Guid.Empty || ingredientId == Guid.Empty)
        {
            throw new BadRequestException(
                "Mỗi phát sinh kho phải gắn với đúng một hàng hóa hoặc nguyên liệu.");
        }

        return new InventoryItemKey(productId, ingredientId);
    }

    private static void EnsureBusiness(Guid businessId)
    {
        if (businessId == Guid.Empty)
        {
            throw new BadRequestException("BusinessId không hợp lệ.");
        }
    }

    private static void EnsureSourceType(string movementType)
    {
        if (movementType is not
            (InventoryMovementTypes.PurchaseIn or InventoryMovementTypes.OrderOut))
        {
            throw new BadRequestException(
                "Nguồn nghiệp vụ chỉ có thể tạo nhập mua hoặc xuất bán.");
        }
    }

    private static MovementHeader NormalizeHeader(
        DateTime occurredAt,
        string? documentNumber,
        string? description)
    {
        documentNumber = documentNumber?.Trim();
        description = description?.Trim();
        if (string.IsNullOrWhiteSpace(documentNumber) || documentNumber.Length > 100)
        {
            throw new BadRequestException(
                "Số chứng từ là bắt buộc và không được vượt quá 100 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(description) || description.Length > 1000)
        {
            throw new BadRequestException(
                "Diễn giải là bắt buộc và không được vượt quá 1000 ký tự.");
        }

        var naiveUtc = BangkokBusinessTime.NormalizeNaiveUtc(occurredAt);
        return new MovementHeader(naiveUtc, documentNumber, description);
    }

    private static InventoryMovement NewMovement(
        Guid businessId,
        InventoryItemKey key,
        string movementType,
        decimal quantity,
        decimal? totalValue,
        MovementHeader header,
        Guid? referenceId)
    {
        return new InventoryMovement
        {
            InventoryMovementId = Guid.NewGuid(),
            BusinessId = businessId,
            ProductId = key.ProductId,
            IngredientId = key.IngredientId,
            MovementType = movementType,
            Quantity = quantity,
            TotalValue = totalValue,
            OccurredAt = header.OccurredAt,
            DocumentNumber = header.DocumentNumber,
            Description = header.Description,
            ReferenceId = referenceId
        };
    }

    private static InventoryItemKey ToKey(InventoryMovement movement) =>
        new(movement.ProductId, movement.IngredientId);

    private sealed class AggregatedLine
    {
        public decimal Quantity { get; set; }

        public decimal? TotalValue { get; set; }
    }

    private sealed record MovementHeader(
        DateTime OccurredAt,
        string DocumentNumber,
        string Description);
}
