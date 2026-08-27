using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.DTO.InventoryPurchase;
using TaxMate.Model.DTO.MoneyMovement;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class InventoryPurchaseServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IExpenseRepository> _expenses = new();
    private readonly Mock<IIngredientPurchaseRepository> _ingredientPurchases = new();
    private readonly Mock<IInventoryPurchaseRepository> _documents = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IIngredientRepository> _ingredients = new();
    private readonly Mock<IInventoryMovementService> _inventoryMovements = new();
    private readonly Mock<IMoneyMovementService> _moneyMovements = new();
    private readonly Mock<ITaxPeriodMutationGuard> _guard = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();
    private readonly Product _product;
    private readonly Ingredient _ingredient;
    private IReadOnlyList<InventoryMovement> _effectiveLedger = [];

    public InventoryPurchaseServiceTests()
    {
        _product = new Product
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            ProductCode = "SP01",
            Name = "Cà phê đóng chai",
            Unit = "chai"
        };
        _ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            Name = "Hạt cà phê",
            Unit = "kg"
        };

        _documents.Setup(x => x.GetBusinessOwnerIdAsync(
                _businessId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_ownerId);
        _documents.Setup(x => x.GetExpenseCategoryAsync(
                _categoryId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpenseCategory
            {
                ExpenseCategoryId = _categoryId,
                BusinessId = _businessId,
                CategoryName = "Nhập hàng"
            });
        _documents.Setup(x => x.GetProductsForWriteAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                ids.Contains(_product.Id) ? [_product] : []);
        _documents.Setup(x => x.GetIngredientsForWriteAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                ids.Contains(_ingredient.Id) ? [_ingredient] : []);
        _documents.Setup(x => x.GetEffectiveLedgerForCacheAsync(
                _businessId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _effectiveLedger);
        _inventoryMovements.Setup(x => x.StageReplaceSourceAsync(
                It.IsAny<ReplaceInventorySourceMovementsCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReplaceInventorySourceMovementsCommand command, CancellationToken _) =>
            {
                var movements = command.Lines.Select(line => new InventoryMovement
                {
                    InventoryMovementId = Guid.NewGuid(),
                    BusinessId = command.BusinessId,
                    ProductId = line.ProductId,
                    IngredientId = line.IngredientId,
                    MovementType = command.MovementType,
                    Quantity = line.Quantity,
                    TotalValue = line.TotalValue,
                    OccurredAt = command.OccurredAt,
                    DocumentNumber = command.DocumentNumber,
                    Description = command.Description,
                    ReferenceId = command.ReferenceId
                }).ToList();
                _effectiveLedger = movements;
                return movements;
            });
        _moneyMovements.Setup(x => x.DeleteAsync(
                _ownerId,
                _businessId,
                MoneyMovementTypes.ExpenseOut,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _moneyMovements.Setup(x => x.SyncAsync(
                It.IsAny<MoneyMovementWriteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoneyMovementWriteResult
            {
                MoneyMovementId = Guid.NewGuid(),
                Outcome = MoneyMovementWriteOutcome.Created
            });
    }

    [Fact]
    public async Task Create_IsAtomic_AggregatesDuplicates_AndRebuildsBothCaches()
    {
        Expense? addedExpense = null;
        IReadOnlyList<IngredientPurchase> addedIngredients = [];
        ReplaceInventorySourceMovementsCommand? movementCommand = null;
        _expenses.Setup(x => x.AddAsync(It.IsAny<Expense>()))
            .Callback<Expense>(x => addedExpense = x)
            .Returns(Task.CompletedTask);
        _ingredientPurchases.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<IngredientPurchase>>()))
            .Callback<IEnumerable<IngredientPurchase>>(x => addedIngredients = x.ToList())
            .Returns(Task.CompletedTask);
        _inventoryMovements.Setup(x => x.StageReplaceSourceAsync(
                It.IsAny<ReplaceInventorySourceMovementsCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback<ReplaceInventorySourceMovementsCommand, CancellationToken>(
                (x, _) => movementCommand = x)
            .ReturnsAsync((ReplaceInventorySourceMovementsCommand command, CancellationToken _) =>
            {
                var result = command.Lines.Select(ToMovement).ToList();
                _effectiveLedger = result;
                return result;
            });

        var response = await CreateService().CreateAsync(
            _ownerId,
            _businessId,
            Request(
                new InventoryPurchaseLineRequest { ProductId = _product.Id, Quantity = 2m, TotalValue = 200m },
                new InventoryPurchaseLineRequest { ProductId = _product.Id, Quantity = 3m, TotalValue = 450m },
                new InventoryPurchaseLineRequest { IngredientId = _ingredient.Id, Quantity = 4m, TotalValue = 160m }));

        Assert.NotNull(addedExpense);
        Assert.StartsWith("PNK-", addedExpense.VoucherNumber);
        Assert.Equal(810m, addedExpense.Amount);
        var ingredientPurchase = Assert.Single(addedIngredients);
        Assert.Equal(addedExpense.ExpenseId, ingredientPurchase.ExpenseId);
        Assert.Equal(addedExpense.VoucherNumber, ingredientPurchase.InvoiceNumber);
        Assert.Equal(4m, ingredientPurchase.Quantity);
        Assert.Equal(160m, ingredientPurchase.TotalCost);
        Assert.Equal(2, movementCommand!.Lines.Count);
        var productLine = Assert.Single(movementCommand.Lines, x => x.ProductId == _product.Id);
        Assert.Equal(5m, productLine.Quantity);
        Assert.Equal(650m, productLine.TotalValue);
        Assert.Equal(5m, _product.StockQuantity);
        Assert.Equal(130m, _product.CostPrice);
        Assert.Equal(4m, _ingredient.StockQuantity);
        Assert.Equal(40m, _ingredient.EstimatedPrice);
        Assert.Equal(2, response.Lines.Count);
        VerifyOneCommit();
    }

    [Fact]
    public async Task Create_Paid_StagesExpenseOutWithSameExpenseReferenceAndAccount()
    {
        var accountId = Guid.NewGuid();
        MoneyMovementWriteRequest? moneyRequest = null;
        _moneyMovements.Setup(x => x.SyncAsync(
                It.IsAny<MoneyMovementWriteRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<MoneyMovementWriteRequest, CancellationToken>((x, _) => moneyRequest = x)
            .ReturnsAsync(new MoneyMovementWriteResult
            {
                MoneyMovementId = Guid.NewGuid(),
                Outcome = MoneyMovementWriteOutcome.Created
            });
        var request = Request(
            new InventoryPurchaseLineRequest { ProductId = _product.Id, Quantity = 2m, TotalValue = 200m });
        request.PaidDate = Utc(2026, 2, 2);
        request.PaymentMethod = PaymentMethods.Transfer;
        request.PaymentAccountId = accountId;

        var response = await CreateService().CreateAsync(_ownerId, _businessId, request);

        Assert.NotNull(moneyRequest);
        Assert.Equal(response.ExpenseId, moneyRequest.ReferenceId);
        Assert.Equal(MoneyMovementTypes.ExpenseOut, moneyRequest.MovementType);
        Assert.Equal(accountId, moneyRequest.PaymentAccountId);
        Assert.Equal(200m, moneyRequest.Amount);
        _moneyMovements.Verify(x => x.DeleteAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        VerifyOneCommit();
    }

    [Fact]
    public async Task Create_WhenMovementFails_RollsBackWithoutSaving()
    {
        _inventoryMovements.Setup(x => x.StageReplaceSourceAsync(
                It.IsAny<ReplaceInventorySourceMovementsCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("movement failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().CreateAsync(
                _ownerId,
                _businessId,
                Request(new InventoryPurchaseLineRequest
                {
                    ProductId = _product.Id,
                    Quantity = 1m,
                    TotalValue = 10m
                })));

        _unitOfWork.Verify(x => x.RollbackTransactionAsync(
            CancellationToken.None), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenPeriodLocked_RollsBackBeforeWritingSource()
    {
        _guard.Setup(x => x.EnsureCanCreateAsync(
                _ownerId,
                _businessId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("locked"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService().CreateAsync(
                _ownerId,
                _businessId,
                Request(new InventoryPurchaseLineRequest
                {
                    ProductId = _product.Id,
                    Quantity = 1m,
                    TotalValue = 10m
                })));

        _expenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Update_GuardsOldAndNewDates_ReplacesSources_AndCommitsOnce()
    {
        var expenseId = Guid.NewGuid();
        var oldIngredientPurchase = new IngredientPurchase
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            BusinessId = _businessId,
            IngredientId = _ingredient.Id,
            Quantity = 1m,
            TotalCost = 40m,
            PurchaseDate = Utc(2026, 1, 5)
        };
        var expense = ExistingExpense(expenseId, oldIngredientPurchase);
        var oldPurchaseDate = expense.ExpenseDate;
        var oldMovements = new List<InventoryMovement>
        {
            Movement(expenseId, null, _ingredient.Id, 1m, 40m, expense.ExpenseDate)
        };
        _documents.Setup(x => x.GetExpenseForWriteAsync(
                expenseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expense);
        _documents.Setup(x => x.GetSourceMovementsAsync(
                _businessId,
                expenseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldMovements);
        var request = UpdateRequest(
            new InventoryPurchaseLineRequest { ProductId = _product.Id, Quantity = 3m, TotalValue = 360m });
        request.PurchaseDate = Utc(2026, 3, 8);

        var result = await CreateService().UpdateAsync(_ownerId, expenseId, request);

        _guard.Verify(x => x.EnsureCanMutateAsync(
            _ownerId,
            _businessId,
            oldPurchaseDate,
            request.PurchaseDate,
            It.IsAny<CancellationToken>()), Times.Once);
        _ingredientPurchases.Verify(x => x.RemoveRange(
            It.Is<IEnumerable<IngredientPurchase>>(items => items.Single() == oldIngredientPurchase)),
            Times.Once);
        Assert.Single(result.Lines);
        Assert.Equal(_product.Id, result.Lines[0].ProductId);
        Assert.Equal(3m, _product.StockQuantity);
        Assert.Equal(0m, _ingredient.StockQuantity);
        VerifyOneCommit();
    }

    [Fact]
    public async Task Delete_RemovesBothLedgersAndSource_ThenZerosCache()
    {
        var expenseId = Guid.NewGuid();
        var ingredientPurchase = new IngredientPurchase
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            BusinessId = _businessId,
            IngredientId = _ingredient.Id,
            Quantity = 2m,
            TotalCost = 80m,
            PurchaseDate = Utc(2026, 1, 5)
        };
        var expense = ExistingExpense(expenseId, ingredientPurchase);
        var movements = new List<InventoryMovement>
        {
            Movement(expenseId, null, _ingredient.Id, 2m, 80m, expense.ExpenseDate)
        };
        _documents.Setup(x => x.GetExpenseForWriteAsync(
                expenseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expense);
        _documents.Setup(x => x.GetSourceMovementsAsync(
                _businessId,
                expenseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(movements);
        _effectiveLedger = [];

        await CreateService().DeleteAsync(_ownerId, expenseId);

        _inventoryMovements.Verify(x => x.StageRemoveSourceAsync(
            _businessId,
            InventoryMovementTypes.PurchaseIn,
            expenseId,
            It.IsAny<CancellationToken>()), Times.Once);
        _moneyMovements.Verify(x => x.DeleteAsync(
            _ownerId,
            _businessId,
            MoneyMovementTypes.ExpenseOut,
            expenseId,
            It.IsAny<CancellationToken>()), Times.Once);
        _expenses.Verify(x => x.Remove(expense), Times.Once);
        Assert.Equal(0m, _ingredient.StockQuantity);
        Assert.Null(_ingredient.EstimatedPrice);
        VerifyOneCommit();
    }

    [Fact]
    public async Task Create_RejectsPaidPurchaseWithoutAccountBeforeAnySourceWrite()
    {
        var request = Request(new InventoryPurchaseLineRequest
        {
            ProductId = _product.Id,
            Quantity = 1m,
            TotalValue = 10m
        });
        request.PaidDate = Utc(2026, 1, 2);
        request.PaymentMethod = PaymentMethods.Cash;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateService().CreateAsync(_ownerId, _businessId, request));

        _expenses.Verify(x => x.AddAsync(It.IsAny<Expense>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(
            CancellationToken.None), Times.Once);
    }

    private InventoryPurchaseService CreateService() => new(
        _unitOfWork.Object,
        _expenses.Object,
        _ingredientPurchases.Object,
        _documents.Object,
        _products.Object,
        _ingredients.Object,
        _inventoryMovements.Object,
        _moneyMovements.Object,
        _guard.Object);

    private CreateInventoryPurchaseRequest Request(
        params InventoryPurchaseLineRequest[] lines) => new()
    {
        ExpenseCategoryId = _categoryId,
        ExpenseTitle = "Nhập hàng tháng 1",
        PurchaseDate = Utc(2026, 1, 5),
        Lines = lines.ToList()
    };

    private UpdateInventoryPurchaseRequest UpdateRequest(
        params InventoryPurchaseLineRequest[] lines) => new()
    {
        ExpenseCategoryId = _categoryId,
        ExpenseTitle = "Phiếu nhập đã sửa",
        PurchaseDate = Utc(2026, 1, 5),
        Lines = lines.ToList()
    };

    private Expense ExistingExpense(
        Guid expenseId,
        params IngredientPurchase[] purchases)
    {
        var expense = new Expense
        {
            ExpenseId = expenseId,
            BusinessId = _businessId,
            ExpenseCategoryId = _categoryId,
            ExpenseCategory = new ExpenseCategory
            {
                ExpenseCategoryId = _categoryId,
                CategoryName = "Nhập hàng",
                BusinessId = _businessId
            },
            VoucherNumber = $"PNK-{expenseId:N}",
            ExpenseTitle = "Old",
            Amount = 40m,
            ExpenseDate = Utc(2026, 1, 5),
            CreatedAt = Utc(2026, 1, 5),
            UpdatedAt = Utc(2026, 1, 5)
        };
        foreach (var purchase in purchases)
        {
            expense.IngredientPurchases.Add(purchase);
        }

        return expense;
    }

    private InventoryMovement ToMovement(InventoryMovementLineInput line) =>
        Movement(
            Guid.NewGuid(),
            line.ProductId,
            line.IngredientId,
            line.Quantity,
            line.TotalValue!.Value,
            Utc(2026, 1, 5));

    private InventoryMovement Movement(
        Guid referenceId,
        Guid? productId,
        Guid? ingredientId,
        decimal quantity,
        decimal value,
        DateTime date) => new()
    {
        InventoryMovementId = Guid.NewGuid(),
        BusinessId = _businessId,
        ProductId = productId,
        IngredientId = ingredientId,
        MovementType = InventoryMovementTypes.PurchaseIn,
        Quantity = quantity,
        TotalValue = value,
        OccurredAt = date,
        DocumentNumber = "PNK-TEST",
        Description = "Nhập hàng",
        ReferenceId = referenceId
    };

    private void VerifyOneCommit()
    {
        _unitOfWork.Verify(x => x.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
}
