using Moq;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;
using TaxMate.Service.Services;

namespace TaxMate.Service.Tests;

public class OrderServiceInventoryTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IPaymentAccountRepository> _paymentAccounts = new();
    private readonly Mock<IGenericRepository<BusinessProfile>> _businessProfiles = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IGenericRepository<ProductPrice>> _productPrices = new();
    private readonly Mock<IGenericRepository<TransactionItem>> _transactionItems = new();
    private readonly Mock<IGenericRepository<Payment>> _payments = new();
    private readonly Mock<IInvoiceService> _invoiceService = new();
    private readonly Mock<IGenericRepository<EInvoiceConfig>> _eInvoiceConfigs = new();
    private readonly Mock<IInvoiceRepository> _invoices = new();
    private readonly Mock<IEInvoiceService> _eInvoiceService = new();
    private readonly Mock<IProductIngredientRepository> _productIngredients = new();
    private readonly Mock<IIngredientRepository> _ingredients = new();

    public OrderServiceInventoryTests()
    {
        _invoiceService
            .Setup(x => x.GenerateFromOrderAsync(It.IsAny<Guid>()))
            .ReturnsAsync("INV-001");
        _invoiceService
            .Setup(x => x.GetInvoiceDetailAsync(It.IsAny<string>()))
            .ReturnsAsync(new InvoiceDetailResponse { InvoiceNumber = "INV-001", Status = InvoiceStatus.Unpaid });
        _transactions
            .Setup(x => x.TryTransitionStatusAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task Checkout_DirectCompletionWithoutBom_DecrementsAggregatedProductStockOnce()
    {
        var productId = Guid.NewGuid();
        var order = CreateOrder(TransactionStatus.Draft,
            CreateItem(productId, 2),
            CreateItem(productId, 3));
        ArrangeOrder(order);
        _productIngredients.Setup(x => x.GetByProductIdAsync(productId)).ReturnsAsync([]);

        await CreateService().CheckoutAsync(order.TransactionId, CashCheckout(order.TotalAmount));

        _transactions.Verify(x => x.TryTransitionStatusAsync(
            order.TransactionId, TransactionStatus.Draft, TransactionStatus.Completed), Times.Once);
        _products.Verify(x => x.DecrementStockAsync(productId, 5), Times.Once);
        _ingredients.Verify(x => x.DecrementStockAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task Checkout_DirectCompletionWithBom_DecrementsOnlyAggregatedIngredientStock()
    {
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var order = CreateOrder(TransactionStatus.Draft,
            CreateItem(firstProductId, 2),
            CreateItem(secondProductId, 3));
        ArrangeOrder(order);
        _productIngredients.Setup(x => x.GetByProductIdAsync(firstProductId)).ReturnsAsync(
        [
            new ProductIngredient { ProductId = firstProductId, IngredientId = ingredientId, Quantity = 1.5m }
        ]);
        _productIngredients.Setup(x => x.GetByProductIdAsync(secondProductId)).ReturnsAsync(
        [
            new ProductIngredient { ProductId = secondProductId, IngredientId = ingredientId, Quantity = 0.5m }
        ]);

        await CreateService().CheckoutAsync(order.TransactionId, CashCheckout(order.TotalAmount));

        _products.Verify(x => x.DecrementStockAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);
        _ingredients.Verify(x => x.DecrementStockAsync(ingredientId, 4.5m), Times.Once);
    }

    [Fact]
    public async Task Checkout_SepayTransfer_TransitionsToAwaitingWithoutDeductingStock()
    {
        var productId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var order = CreateOrder(TransactionStatus.Draft, CreateItem(productId, 2));
        ArrangeOrder(order);
        _paymentAccounts.Setup(x => x.GetByIdAsync(accountId)).ReturnsAsync(new PaymentAccount
        {
            PaymentAccountId = accountId,
            BusinessId = order.BusinessId,
            SePayBankAccountXid = "sepay-account"
        });

        await CreateService().CheckoutAsync(order.TransactionId, new CheckoutRequest
        {
            Payments =
            [
                new PaymentEntry
                {
                    PaymentMethod = "Transfer",
                    PaymentAccountId = accountId,
                    Amount = order.TotalAmount
                }
            ]
        });

        _transactions.Verify(x => x.TryTransitionStatusAsync(
            order.TransactionId, TransactionStatus.Draft, TransactionStatus.AwaitingPayment), Times.Once);
        _products.Verify(x => x.DecrementStockAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);
        _ingredients.Verify(x => x.DecrementStockAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);
        Assert.Equal(TransactionStatus.AwaitingPayment, order.Status);
    }

    [Fact]
    public async Task ConfirmPayment_WinningTransition_DecrementsInventoryOnce()
    {
        var productId = Guid.NewGuid();
        var order = CreateOrder(TransactionStatus.AwaitingPayment, CreateItem(productId, 4));
        order.InvoiceId = "INV-001";
        order.Payments.Add(new Payment { PaymentId = Guid.NewGuid(), PaidAt = null });
        ArrangeOrder(order);
        _productIngredients.Setup(x => x.GetByProductIdAsync(productId)).ReturnsAsync([]);

        await CreateService().ConfirmPaymentAsync(order.TransactionId);

        _transactions.Verify(x => x.TryTransitionStatusAsync(
            order.TransactionId, TransactionStatus.AwaitingPayment, TransactionStatus.Completed), Times.Once);
        _products.Verify(x => x.DecrementStockAsync(productId, 4), Times.Once);
        Assert.Equal(TransactionStatus.Completed, order.Status);
        Assert.NotNull(order.Payments.Single().PaidAt);
    }

    [Fact]
    public async Task ConfirmPayment_LosingTransition_RollsBackWithoutDeductingInventory()
    {
        var productId = Guid.NewGuid();
        var order = CreateOrder(TransactionStatus.AwaitingPayment, CreateItem(productId, 4));
        order.InvoiceId = "INV-001";
        ArrangeOrder(order);
        _transactions.Setup(x => x.TryTransitionStatusAsync(
                order.TransactionId, TransactionStatus.AwaitingPayment, TransactionStatus.Completed))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService().ConfirmPaymentAsync(order.TransactionId));

        _products.Verify(x => x.DecrementStockAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);
        _ingredients.Verify(x => x.DecrementStockAsync(It.IsAny<Guid>(), It.IsAny<decimal>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Checkout_DownstreamFailure_RollsBackClaimAndInventoryTransaction()
    {
        var productId = Guid.NewGuid();
        var order = CreateOrder(TransactionStatus.Draft, CreateItem(productId, 2));
        ArrangeOrder(order);
        _productIngredients.Setup(x => x.GetByProductIdAsync(productId)).ReturnsAsync([]);
        _invoiceService.Setup(x => x.GenerateFromOrderAsync(order.TransactionId))
            .ThrowsAsync(new InvalidOperationException("invoice failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().CheckoutAsync(order.TransactionId, CashCheckout(order.TotalAmount)));

        _products.Verify(x => x.DecrementStockAsync(productId, 2), Times.Once);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddItem_NonPositiveQuantity_IsRejectedBeforeProductLookup(decimal quantity)
    {
        var order = CreateOrder(TransactionStatus.Draft);
        ArrangeOrder(order);

        await Assert.ThrowsAsync<BadRequestException>(() => CreateService().AddItemAsync(
            order.TransactionId,
            new AddOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = quantity }));

        _products.Verify(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Product, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task Checkout_LegacyNonPositiveItem_IsRejectedBeforeTransactionStarts()
    {
        var order = CreateOrder(TransactionStatus.Draft, CreateItem(Guid.NewGuid(), 0));
        ArrangeOrder(order);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            CreateService().CheckoutAsync(order.TransactionId, CashCheckout(order.TotalAmount)));

        _unitOfWork.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _transactions.Verify(x => x.TryTransitionStatusAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private void ArrangeOrder(Transaction order)
    {
        _transactions.Setup(x => x.GetByIdWithDetailsAsync(order.TransactionId)).ReturnsAsync(order);
    }

    private static Transaction CreateOrder(string status, params TransactionItem[] items)
    {
        var order = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            BusinessId = Guid.NewGuid(),
            TransactionCode = "ORDER-001",
            Status = status,
            TotalAmount = 100,
            TransactionItems = items.ToList(),
            Payments = []
        };

        foreach (var item in order.TransactionItems)
        {
            item.TransactionId = order.TransactionId;
        }

        return order;
    }

    private static TransactionItem CreateItem(Guid productId, decimal quantity) => new()
    {
        TransactionItemId = Guid.NewGuid(),
        ProductId = productId,
        ProductName = "Product",
        Quantity = quantity
    };

    private static CheckoutRequest CashCheckout(decimal amount) => new()
    {
        Payments = [new PaymentEntry { PaymentMethod = "Cash", Amount = amount }]
    };

    private OrderService CreateService() => new(
        _unitOfWork.Object,
        _transactions.Object,
        _paymentAccounts.Object,
        _businessProfiles.Object,
        _products.Object,
        _productPrices.Object,
        _transactionItems.Object,
        _payments.Object,
        _invoiceService.Object,
        _eInvoiceConfigs.Object,
        _invoices.Object,
        _eInvoiceService.Object,
        _productIngredients.Object,
        _ingredients.Object);
}
