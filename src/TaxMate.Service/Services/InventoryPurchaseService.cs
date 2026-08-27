using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.DTO.InventoryPurchase;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Common;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

internal sealed class InventoryPurchaseService : IInventoryPurchaseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExpenseRepository _expenses;
    private readonly IIngredientPurchaseRepository _ingredientPurchases;
    private readonly IInventoryPurchaseRepository _documents;
    private readonly IProductRepository _products;
    private readonly IIngredientRepository _ingredients;
    private readonly IInventoryMovementService _inventoryMovements;
    private readonly IMoneyMovementService _moneyMovements;
    private readonly ITaxPeriodMutationGuard _periodGuard;

    public InventoryPurchaseService(
        IUnitOfWork unitOfWork,
        IExpenseRepository expenses,
        IIngredientPurchaseRepository ingredientPurchases,
        IInventoryPurchaseRepository documents,
        IProductRepository products,
        IIngredientRepository ingredients,
        IInventoryMovementService inventoryMovements,
        IMoneyMovementService moneyMovements,
        ITaxPeriodMutationGuard periodGuard)
    {
        _unitOfWork = unitOfWork;
        _expenses = expenses;
        _ingredientPurchases = ingredientPurchases;
        _documents = documents;
        _products = products;
        _ingredients = ingredients;
        _inventoryMovements = inventoryMovements;
        _moneyMovements = moneyMovements;
        _periodGuard = periodGuard;
    }

    public async Task<InventoryPurchaseResponse> CreateAsync(
        Guid ownerId,
        Guid businessId,
        CreateInventoryPurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var purchaseDate = NormalizeRequiredDate(request.PurchaseDate, "Ngày nhập hàng");
        var paidDate = NormalizeOptionalDate(request.PaidDate);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await EnsureOwnerAsync(ownerId, businessId, cancellationToken);
            await _periodGuard.EnsureCanCreateAsync(
                ownerId,
                businessId,
                purchaseDate,
                cancellationToken);
            if (paidDate.HasValue && paidDate.Value != purchaseDate)
            {
                await _periodGuard.EnsureCanCreateAsync(
                    ownerId,
                    businessId,
                    paidDate.Value,
                    cancellationToken);
            }

            var validated = await ValidateRequestAsync(
                businessId,
                request,
                purchaseDate,
                paidDate,
                existingItemKeys: new HashSet<InventoryItemKey>(),
                cancellationToken);
            var now = DateTime.UtcNow;
            var expenseId = Guid.NewGuid();
            var expense = NewExpense(expenseId, businessId, validated, now);
            await _expenses.AddAsync(expense);

            var ingredientLines = NewIngredientPurchases(
                expense,
                validated,
                now);
            await _ingredientPurchases.AddRangeAsync(ingredientLines);

            var stagedMovements = await StageMovementsAsync(
                expense,
                validated,
                cancellationToken);
            StampMovements(stagedMovements, now);
            await RebuildCachesAsync(
                businessId,
                validated.Lines.Keys.ToHashSet(),
                cancellationToken);
            await SyncMoneyMovementAsync(
                ownerId,
                expense,
                validated,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return MapResponse(
                expense,
                stagedMovements,
                validated.PaymentAccountId,
                validated.Products,
                validated.Ingredients);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<InventoryPurchaseResponse> UpdateAsync(
        Guid ownerId,
        Guid expenseId,
        UpdateInventoryPurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var purchaseDate = NormalizeRequiredDate(request.PurchaseDate, "Ngày nhập hàng");
        var paidDate = NormalizeOptionalDate(request.PaidDate);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var expense = await _documents.GetExpenseForWriteAsync(
                expenseId,
                cancellationToken);
            if (expense is null)
            {
                throw new NotFoundException("Không tìm thấy phiếu nhập hàng.");
            }

            await EnsureOwnerAsync(ownerId, expense.BusinessId, cancellationToken);
            var oldMovements = await _documents.GetSourceMovementsAsync(
                expense.BusinessId,
                expenseId,
                cancellationToken);
            EnsurePurchaseSource(oldMovements);
            await GuardUpdateDatesAsync(
                ownerId,
                expense.BusinessId,
                expense.ExpenseDate,
                purchaseDate,
                expense.PaidDate,
                paidDate,
                cancellationToken);

            var oldKeys = oldMovements.Select(ToKey).ToHashSet();
            var validated = await ValidateRequestAsync(
                expense.BusinessId,
                request,
                purchaseDate,
                paidDate,
                oldKeys,
                cancellationToken);
            var now = DateTime.UtcNow;
            ApplyExpense(expense, validated, now);

            _ingredientPurchases.RemoveRange(expense.IngredientPurchases.ToList());
            expense.IngredientPurchases.Clear();
            var ingredientLines = NewIngredientPurchases(
                expense,
                validated,
                now);
            await _ingredientPurchases.AddRangeAsync(ingredientLines);

            var stagedMovements = await StageMovementsAsync(
                expense,
                validated,
                cancellationToken);
            StampMovements(stagedMovements, now);
            await RebuildCachesAsync(
                expense.BusinessId,
                oldKeys.Concat(validated.Lines.Keys).ToHashSet(),
                cancellationToken);
            await SyncMoneyMovementAsync(
                ownerId,
                expense,
                validated,
                cancellationToken);

            _expenses.Update(expense);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return MapResponse(
                expense,
                stagedMovements,
                validated.PaymentAccountId,
                validated.Products,
                validated.Ingredients);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteAsync(
        Guid ownerId,
        Guid expenseId,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var expense = await _documents.GetExpenseForWriteAsync(
                expenseId,
                cancellationToken);
            if (expense is null)
            {
                throw new NotFoundException("Không tìm thấy phiếu nhập hàng.");
            }

            await EnsureOwnerAsync(ownerId, expense.BusinessId, cancellationToken);
            var oldMovements = await _documents.GetSourceMovementsAsync(
                expense.BusinessId,
                expenseId,
                cancellationToken);
            EnsurePurchaseSource(oldMovements);
            await _periodGuard.EnsureCanDeleteAsync(
                ownerId,
                expense.BusinessId,
                expense.ExpenseDate,
                cancellationToken);
            if (expense.PaidDate.HasValue && expense.PaidDate.Value != expense.ExpenseDate)
            {
                await _periodGuard.EnsureCanDeleteAsync(
                    ownerId,
                    expense.BusinessId,
                    expense.PaidDate.Value,
                    cancellationToken);
            }

            await _inventoryMovements.StageRemoveSourceAsync(
                expense.BusinessId,
                InventoryMovementTypes.PurchaseIn,
                expense.ExpenseId,
                cancellationToken);
            await _moneyMovements.DeleteAsync(
                ownerId,
                expense.BusinessId,
                MoneyMovementTypes.ExpenseOut,
                expense.ExpenseId,
                cancellationToken);
            _ingredientPurchases.RemoveRange(expense.IngredientPurchases.ToList());
            _expenses.Remove(expense);
            await RebuildCachesAsync(
                expense.BusinessId,
                oldMovements.Select(ToKey).ToHashSet(),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<InventoryPurchaseResponse> GetByIdAsync(
        Guid ownerId,
        Guid expenseId,
        CancellationToken cancellationToken = default)
    {
        var expense = await _documents.GetExpenseForReadAsync(
            expenseId,
            cancellationToken);
        if (expense is null)
        {
            throw new NotFoundException("Không tìm thấy phiếu nhập hàng.");
        }

        await EnsureOwnerAsync(ownerId, expense.BusinessId, cancellationToken);
        var movements = await _documents.GetSourceMovementsAsync(
            expense.BusinessId,
            expenseId,
            cancellationToken);
        EnsurePurchaseSource(movements);
        var money = await _documents.GetExpenseMoneyMovementAsync(
            expenseId,
            cancellationToken);
        return MapResponse(expense, movements, money?.PaymentAccountId);
    }

    public async Task<PagedResult<InventoryPurchaseResponse>> GetPagedAsync(
        Guid ownerId,
        Guid businessId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1 || pageSize is < 1 or > 100)
        {
            throw new BadRequestException("Phân trang không hợp lệ.");
        }

        await EnsureOwnerAsync(ownerId, businessId, cancellationToken);
        var (items, totalCount) = await _documents.GetPagedAsync(
            businessId,
            pageNumber,
            pageSize,
            cancellationToken);
        var responses = new List<InventoryPurchaseResponse>(items.Count);
        foreach (var expense in items)
        {
            var movements = await _documents.GetSourceMovementsAsync(
                businessId,
                expense.ExpenseId,
                cancellationToken);
            var money = await _documents.GetExpenseMoneyMovementAsync(
                expense.ExpenseId,
                cancellationToken);
            responses.Add(MapResponse(expense, movements, money?.PaymentAccountId));
        }

        return new PagedResult<InventoryPurchaseResponse>
        {
            Items = responses,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    private async Task<ValidatedPurchase> ValidateRequestAsync(
        Guid businessId,
        InventoryPurchaseWriteRequest request,
        DateTime purchaseDate,
        DateTime? paidDate,
        IReadOnlySet<InventoryItemKey> existingItemKeys,
        CancellationToken cancellationToken)
    {
        if (request.ExpenseCategoryId == Guid.Empty)
        {
            throw new BadRequestException("Nhóm chi phí là bắt buộc.");
        }

        var category = await _documents.GetExpenseCategoryAsync(
            request.ExpenseCategoryId,
            cancellationToken);
        if (category is null ||
            (category.BusinessId.HasValue && category.BusinessId != businessId))
        {
            throw new BadRequestException("Nhóm chi phí không thuộc cửa hàng này.");
        }

        Supplier? supplier = null;
        if (request.SupplierId.HasValue)
        {
            if (request.SupplierId.Value == Guid.Empty)
            {
                throw new BadRequestException("Nhà cung cấp không hợp lệ.");
            }

            supplier = await _documents.GetSupplierAsync(
                request.SupplierId.Value,
                cancellationToken);
            if (supplier is null || supplier.BusinessId != businessId)
            {
                throw new BadRequestException("Nhà cung cấp không thuộc cửa hàng này.");
            }
        }

        var title = request.ExpenseTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            throw new BadRequestException("Tên phiếu nhập là bắt buộc và tối đa 200 ký tự.");
        }

        var voucherNumber = NormalizePurchaseVoucherNumber(request.VoucherNumber);

        var lines = AggregateLines(request.Lines);
        var productIds = lines.Keys
            .Where(x => x.ProductId.HasValue)
            .Select(x => x.ProductId!.Value)
            .ToArray();
        var ingredientIds = lines.Keys
            .Where(x => x.IngredientId.HasValue)
            .Select(x => x.IngredientId!.Value)
            .ToArray();
        var products = await _documents.GetProductsForWriteAsync(
            productIds,
            cancellationToken);
        var ingredients = await _documents.GetIngredientsForWriteAsync(
            ingredientIds,
            cancellationToken);
        var productsById = products.ToDictionary(x => x.Id);
        var ingredientsById = ingredients.ToDictionary(x => x.Id);

        foreach (var productId in productIds)
        {
            var key = InventoryItemKey.ForProduct(productId);
            if (!productsById.TryGetValue(productId, out var product) ||
                product.BusinessId != businessId ||
                (product.IsDeleted && !existingItemKeys.Contains(key)))
            {
                throw new BadRequestException("Hàng hóa không tồn tại hoặc không thuộc cửa hàng này.");
            }
        }

        foreach (var ingredientId in ingredientIds)
        {
            var key = InventoryItemKey.ForIngredient(ingredientId);
            if (!ingredientsById.TryGetValue(ingredientId, out var ingredient) ||
                ingredient.BusinessId != businessId ||
                (ingredient.IsDeleted && !existingItemKeys.Contains(key)))
            {
                throw new BadRequestException("Nguyên liệu không tồn tại hoặc không thuộc cửa hàng này.");
            }
        }

        var (paymentMethod, paymentAccountId) = ValidatePayment(
            paidDate,
            request.PaymentMethod,
            request.PaymentAccountId);
        return new ValidatedPurchase
        {
            ExpenseCategory = category,
            VoucherNumber = voucherNumber,
            ExpenseTitle = title,
            PurchaseDate = purchaseDate,
            Supplier = supplier,
            ReceiptImageUrl = NormalizeOptionalText(
                request.ReceiptImageUrl,
                1000,
                "Ảnh chứng từ"),
            FileUrl = NormalizeOptionalText(request.FileUrl, 1000, "Tệp chứng từ"),
            Note = NormalizeOptionalText(request.Note, 2000, "Ghi chú"),
            DueDate = NormalizeOptionalDate(request.DueDate),
            PaidDate = paidDate,
            PaymentMethod = paymentMethod,
            PaymentAccountId = paymentAccountId,
            Lines = lines,
            Products = productsById,
            Ingredients = ingredientsById
        };
    }

    private static Dictionary<InventoryItemKey, AggregatedPurchaseLine> AggregateLines(
        IReadOnlyCollection<InventoryPurchaseLineRequest>? requestLines)
    {
        if (requestLines is null || requestLines.Count == 0)
        {
            throw new BadRequestException("Phiếu nhập phải có ít nhất một mặt hàng.");
        }

        var lines = new Dictionary<InventoryItemKey, AggregatedPurchaseLine>();
        foreach (var line in requestLines)
        {
            if (line is null ||
                line.ProductId.HasValue == line.IngredientId.HasValue ||
                line.ProductId == Guid.Empty ||
                line.IngredientId == Guid.Empty)
            {
                throw new BadRequestException(
                    "Mỗi dòng phải gắn với đúng một hàng hóa hoặc nguyên liệu.");
            }

            if (line.Quantity <= 0 || line.TotalValue <= 0)
            {
                throw new BadRequestException("Số lượng và giá trị dòng nhập phải lớn hơn 0.");
            }

            var key = new InventoryItemKey(line.ProductId, line.IngredientId);
            if (!lines.TryGetValue(key, out var aggregate))
            {
                aggregate = new AggregatedPurchaseLine();
                lines.Add(key, aggregate);
            }

            aggregate.Quantity += line.Quantity;
            aggregate.TotalValue += line.TotalValue;
        }

        return lines;
    }

    private static (string? PaymentMethod, Guid? PaymentAccountId) ValidatePayment(
        DateTime? paidDate,
        string? paymentMethod,
        Guid? paymentAccountId)
    {
        if (!paidDate.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(paymentMethod) || paymentAccountId.HasValue)
            {
                throw new BadRequestException(
                    "Chỉ chọn phương thức và tài khoản khi phiếu đã thanh toán.");
            }

            return (null, null);
        }

        if (!paymentAccountId.HasValue || paymentAccountId.Value == Guid.Empty)
        {
            throw new BadRequestException("Tài khoản thanh toán là bắt buộc.");
        }

        var normalized = paymentMethod?.Trim();
        if (string.Equals(normalized, PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase))
        {
            normalized = PaymentMethods.Cash;
        }
        else if (string.Equals(normalized, PaymentMethods.Transfer, StringComparison.OrdinalIgnoreCase))
        {
            normalized = PaymentMethods.Transfer;
        }
        else
        {
            throw new BadRequestException("Phương thức thanh toán phải là Cash hoặc Transfer.");
        }

        return (normalized, paymentAccountId);
    }

    private static Expense NewExpense(
        Guid expenseId,
        Guid businessId,
        ValidatedPurchase validated,
        DateTime now)
    {
        var expense = new Expense
        {
            ExpenseId = expenseId,
            BusinessId = businessId,
            VoucherNumber = validated.VoucherNumber ?? BuildPurchaseVoucherNumber(
                validated.PurchaseDate,
                expenseId),
            CreatedAt = now
        };
        ApplyExpense(expense, validated, now);
        return expense;
    }

    private static string? NormalizePurchaseVoucherNumber(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.StartsWith("PNK-", StringComparison.OrdinalIgnoreCase)
            ? $"PNK-{value[4..]}"
            : $"PNK-{value}";
        if (normalized.Length > 100)
        {
            throw new BadRequestException(
                "Số chứng từ nhập kho không được vượt quá 100 ký tự.");
        }

        return normalized;
    }

    private static string BuildPurchaseVoucherNumber(
        DateTime purchaseDate,
        Guid expenseId)
        => $"PNK-{purchaseDate:yyMMdd}-{expenseId:N}"[..21].ToUpperInvariant();

    private static void ApplyExpense(
        Expense expense,
        ValidatedPurchase validated,
        DateTime now)
    {
        expense.ExpenseCategoryId = validated.ExpenseCategory.ExpenseCategoryId;
        expense.ExpenseCategory = validated.ExpenseCategory;
        expense.ExpenseTitle = validated.ExpenseTitle;
        expense.Amount = validated.Lines.Values.Sum(x => x.TotalValue);
        expense.ExpenseDate = validated.PurchaseDate;
        expense.PaymentMethod = validated.PaymentMethod;
        expense.ReceiptImageUrl = validated.ReceiptImageUrl;
        expense.Note = validated.Note;
        expense.FileUrl = validated.FileUrl;
        expense.DueDate = validated.DueDate;
        expense.PaidDate = validated.PaidDate;
        expense.SupplierId = validated.Supplier?.Id;
        expense.Supplier = validated.Supplier;
        expense.UpdatedAt = now;
    }

    private static IReadOnlyList<IngredientPurchase> NewIngredientPurchases(
        Expense expense,
        ValidatedPurchase validated,
        DateTime now)
    {
        var result = validated.Lines
            .Where(x => x.Key.IngredientId.HasValue)
            .Select(x => new IngredientPurchase
            {
                Id = Guid.NewGuid(),
                BusinessId = expense.BusinessId,
                ExpenseId = expense.ExpenseId,
                Expense = expense,
                IngredientId = x.Key.IngredientId!.Value,
                Ingredient = validated.Ingredients[x.Key.IngredientId.Value],
                Quantity = x.Value.Quantity,
                TotalCost = x.Value.TotalValue,
                PurchaseDate = validated.PurchaseDate,
                InvoiceNumber = expense.VoucherNumber,
                SupplierId = validated.Supplier?.Id,
                Supplier = validated.Supplier,
                SupplierName = validated.Supplier?.Name,
                ReceiptImageUrl = validated.ReceiptImageUrl,
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToList();
        foreach (var line in result)
        {
            expense.IngredientPurchases.Add(line);
        }

        return result;
    }

    private async Task<IReadOnlyList<InventoryMovement>> StageMovementsAsync(
        Expense expense,
        ValidatedPurchase validated,
        CancellationToken cancellationToken)
    {
        return await _inventoryMovements.StageReplaceSourceAsync(
            new ReplaceInventorySourceMovementsCommand
            {
                BusinessId = expense.BusinessId,
                MovementType = InventoryMovementTypes.PurchaseIn,
                ReferenceId = expense.ExpenseId,
                OccurredAt = validated.PurchaseDate,
                DocumentNumber = expense.VoucherNumber,
                Description = validated.ExpenseTitle,
                Lines = validated.Lines.Select(x => new InventoryMovementLineInput
                {
                    ProductId = x.Key.ProductId,
                    IngredientId = x.Key.IngredientId,
                    Quantity = x.Value.Quantity,
                    TotalValue = x.Value.TotalValue
                }).ToList()
            },
            cancellationToken);
    }

    private async Task SyncMoneyMovementAsync(
        Guid ownerId,
        Expense expense,
        ValidatedPurchase validated,
        CancellationToken cancellationToken)
    {
        if (!validated.PaidDate.HasValue)
        {
            await _moneyMovements.DeleteAsync(
                ownerId,
                expense.BusinessId,
                MoneyMovementTypes.ExpenseOut,
                expense.ExpenseId,
                cancellationToken);
            return;
        }

        await _moneyMovements.SyncAsync(
            new MoneyMovementWriteRequest
            {
                OwnerId = ownerId,
                BusinessId = expense.BusinessId,
                PaymentAccountId = validated.PaymentAccountId!.Value,
                PaymentMethod = validated.PaymentMethod!,
                MovementType = MoneyMovementTypes.ExpenseOut,
                Amount = expense.Amount,
                MovementDate = validated.PaidDate.Value,
                DocumentNumber = expense.VoucherNumber,
                Description = $"Chi tiền nhập hàng - {validated.ExpenseTitle}",
                ReferenceId = expense.ExpenseId
            },
            cancellationToken);
    }

    private async Task RebuildCachesAsync(
        Guid businessId,
        IReadOnlySet<InventoryItemKey> itemKeys,
        CancellationToken cancellationToken)
    {
        var productIds = itemKeys
            .Where(x => x.ProductId.HasValue)
            .Select(x => x.ProductId!.Value)
            .Distinct()
            .ToArray();
        var ingredientIds = itemKeys
            .Where(x => x.IngredientId.HasValue)
            .Select(x => x.IngredientId!.Value)
            .Distinct()
            .ToArray();
        var ledger = await _documents.GetEffectiveLedgerForCacheAsync(
            businessId,
            productIds,
            ingredientIds,
            cancellationToken);
        var snapshots = InventoryLedgerCacheProjector.Project(ledger);
        var products = await _documents.GetProductsForWriteAsync(
            productIds,
            cancellationToken);
        var ingredients = await _documents.GetIngredientsForWriteAsync(
            ingredientIds,
            cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var product in products)
        {
            var snapshot = snapshots.GetValueOrDefault(
                InventoryItemKey.ForProduct(product.Id),
                new InventoryCacheSnapshot(0m, null));
            product.StockQuantity = snapshot.Quantity;
            product.CostPrice = snapshot.UnitValue;
            product.UpdatedAt = now;
            _products.Update(product);
        }

        foreach (var ingredient in ingredients)
        {
            var snapshot = snapshots.GetValueOrDefault(
                InventoryItemKey.ForIngredient(ingredient.Id),
                new InventoryCacheSnapshot(0m, null));
            ingredient.StockQuantity = snapshot.Quantity;
            ingredient.EstimatedPrice = snapshot.UnitValue;
            ingredient.UpdatedAt = now;
            _ingredients.Update(ingredient);
        }
    }

    private async Task GuardUpdateDatesAsync(
        Guid ownerId,
        Guid businessId,
        DateTime oldPurchaseDate,
        DateTime newPurchaseDate,
        DateTime? oldPaidDate,
        DateTime? newPaidDate,
        CancellationToken cancellationToken)
    {
        await _periodGuard.EnsureCanMutateAsync(
            ownerId,
            businessId,
            oldPurchaseDate,
            newPurchaseDate,
            cancellationToken);
        if (oldPaidDate.HasValue && newPaidDate.HasValue)
        {
            await _periodGuard.EnsureCanMutateAsync(
                ownerId,
                businessId,
                oldPaidDate.Value,
                newPaidDate.Value,
                cancellationToken);
        }
        else if (oldPaidDate.HasValue)
        {
            await _periodGuard.EnsureCanDeleteAsync(
                ownerId,
                businessId,
                oldPaidDate.Value,
                cancellationToken);
        }
        else if (newPaidDate.HasValue)
        {
            await _periodGuard.EnsureCanCreateAsync(
                ownerId,
                businessId,
                newPaidDate.Value,
                cancellationToken);
        }
    }

    private async Task EnsureOwnerAsync(
        Guid ownerId,
        Guid businessId,
        CancellationToken cancellationToken)
    {
        if (ownerId == Guid.Empty || businessId == Guid.Empty)
        {
            throw new BadRequestException("Owner hoặc BusinessId không hợp lệ.");
        }

        var actualOwner = await _documents.GetBusinessOwnerIdAsync(
            businessId,
            cancellationToken);
        if (!actualOwner.HasValue)
        {
            throw new NotFoundException("Không tìm thấy cửa hàng.");
        }

        if (actualOwner.Value != ownerId)
        {
            throw new ForbiddenException();
        }
    }

    private static void EnsurePurchaseSource(
        IReadOnlyCollection<InventoryMovement> movements)
    {
        if (movements.Count == 0)
        {
            throw new NotFoundException(
                "Chứng từ chi này không phải phiếu nhập được quản lý bởi sổ kho.");
        }
    }

    private static void StampMovements(
        IEnumerable<InventoryMovement> movements,
        DateTime now)
    {
        foreach (var movement in movements)
        {
            if (movement.CreatedAt == default)
            {
                movement.CreatedAt = now;
            }

            movement.UpdatedAt = now;
        }
    }

    private static DateTime NormalizeRequiredDate(DateTime value, string fieldName)
    {
        if (value == default)
        {
            throw new BadRequestException($"{fieldName} là bắt buộc.");
        }

        return BangkokBusinessTime.NormalizeNaiveUtc(value);
    }

    private static DateTime? NormalizeOptionalDate(DateTime? value) =>
        value.HasValue
            ? NormalizeRequiredDate(value.Value, "Ngày")
            : null;

    private static string? NormalizeOptionalText(
        string? value,
        int maximumLength,
        string fieldName)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length > maximumLength)
        {
            throw new BadRequestException($"{fieldName} tối đa {maximumLength} ký tự.");
        }

        return value;
    }

    private static InventoryItemKey ToKey(InventoryMovement movement) =>
        new(movement.ProductId, movement.IngredientId);

    private static InventoryPurchaseResponse MapResponse(
        Expense expense,
        IReadOnlyCollection<InventoryMovement> movements,
        Guid? paymentAccountId,
        IReadOnlyDictionary<Guid, Product>? products = null,
        IReadOnlyDictionary<Guid, Ingredient>? ingredients = null)
    {
        return new InventoryPurchaseResponse
        {
            ExpenseId = expense.ExpenseId,
            BusinessId = expense.BusinessId,
            ExpenseCategoryId = expense.ExpenseCategoryId,
            ExpenseCategoryName = expense.ExpenseCategory?.CategoryName,
            VoucherNumber = expense.VoucherNumber,
            ExpenseTitle = expense.ExpenseTitle,
            Amount = expense.Amount,
            PurchaseDate = expense.ExpenseDate,
            SupplierId = expense.SupplierId,
            SupplierName = expense.Supplier?.Name,
            ReceiptImageUrl = expense.ReceiptImageUrl,
            FileUrl = expense.FileUrl,
            Note = expense.Note,
            DueDate = expense.DueDate,
            PaidDate = expense.PaidDate,
            PaymentMethod = expense.PaymentMethod,
            PaymentAccountId = paymentAccountId,
            Lines = movements.Select(movement => new InventoryPurchaseLineResponse
            {
                ProductId = movement.ProductId,
                IngredientId = movement.IngredientId,
                ItemName = movement.Product?.Name ??
                    movement.Ingredient?.Name ??
                    (movement.ProductId.HasValue && products is not null
                        ? products[movement.ProductId.Value].Name
                        : movement.IngredientId.HasValue && ingredients is not null
                            ? ingredients[movement.IngredientId.Value].Name
                            : string.Empty),
                Unit = movement.Product?.Unit ??
                    movement.Ingredient?.Unit ??
                    (movement.ProductId.HasValue && products is not null
                        ? products[movement.ProductId.Value].Unit
                        : movement.IngredientId.HasValue && ingredients is not null
                            ? ingredients[movement.IngredientId.Value].Unit
                            : null),
                Quantity = movement.Quantity,
                TotalValue = movement.TotalValue ?? 0m
            }).ToList(),
            CreatedAt = expense.CreatedAt,
            UpdatedAt = expense.UpdatedAt
        };
    }

    private sealed class AggregatedPurchaseLine
    {
        public decimal Quantity { get; set; }

        public decimal TotalValue { get; set; }
    }

    private sealed class ValidatedPurchase
    {
        public ExpenseCategory ExpenseCategory { get; init; } = null!;

        public string? VoucherNumber { get; init; }

        public string ExpenseTitle { get; init; } = null!;

        public DateTime PurchaseDate { get; init; }

        public Supplier? Supplier { get; init; }

        public string? ReceiptImageUrl { get; init; }

        public string? FileUrl { get; init; }

        public string? Note { get; init; }

        public DateTime? DueDate { get; init; }

        public DateTime? PaidDate { get; init; }

        public string? PaymentMethod { get; init; }

        public Guid? PaymentAccountId { get; init; }

        public IReadOnlyDictionary<InventoryItemKey, AggregatedPurchaseLine> Lines { get; init; }
            = new Dictionary<InventoryItemKey, AggregatedPurchaseLine>();

        public IReadOnlyDictionary<Guid, Product> Products { get; init; }
            = new Dictionary<Guid, Product>();

        public IReadOnlyDictionary<Guid, Ingredient> Ingredients { get; init; }
            = new Dictionary<Guid, Ingredient>();
    }
}
