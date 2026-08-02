using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Model.Entities;
using TaxMate.Repository.Interfaces;
using TaxMate.Service.Exceptions;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionRepository _transactions;
    private readonly IPaymentAccountRepository _paymentAccounts;
    private readonly IGenericRepository<BusinessProfile> _businessProfiles;
    private readonly IProductRepository _products;
    private readonly IGenericRepository<ProductPrice> _productPrices;
    private readonly IGenericRepository<TransactionItem> _transactionItems;
    private readonly IGenericRepository<Payment> _payments;
    private readonly IInvoiceService _invoiceService;
    private readonly IGenericRepository<EInvoiceConfig> _eInvoiceConfigs;
    private readonly IInvoiceRepository _invoices;
    private readonly IEInvoiceService _eInvoiceService;
    private readonly IProductIngredientRepository _productIngredients;
    private readonly IIngredientRepository _ingredients;

    public OrderService(
        IUnitOfWork unitOfWork,
        ITransactionRepository transactions,
        IPaymentAccountRepository paymentAccounts,
        IGenericRepository<BusinessProfile> businessProfiles,
        IProductRepository products,
        IGenericRepository<ProductPrice> productPrices,
        IGenericRepository<TransactionItem> transactionItems,
        IGenericRepository<Payment> payments,
        IInvoiceService invoiceService,
        IGenericRepository<EInvoiceConfig> eInvoiceConfigs,
        IInvoiceRepository invoices,
        IEInvoiceService eInvoiceService,
        IProductIngredientRepository productIngredients,
        IIngredientRepository ingredients)
    {
        _unitOfWork = unitOfWork;
        _transactions = transactions;
        _paymentAccounts = paymentAccounts;
        _businessProfiles = businessProfiles;
        _products = products;
        _productPrices = productPrices;
        _transactionItems = transactionItems;
        _payments = payments;
        _invoiceService = invoiceService;
        _eInvoiceConfigs = eInvoiceConfigs;
        _invoices = invoices;
        _eInvoiceService = eInvoiceService;
        _productIngredients = productIngredients;
        _ingredients = ingredients;
    }

    public async Task<Guid> CreateOrderAsync(Guid businessId, CreateOrderRequest request)
    {
        var business = await _businessProfiles.GetByIdAsync(businessId);
        if (business == null)
        {
            throw new NotFoundException("Business profile not found.");
        }

        var code = await _transactions.GenerateTransactionCodeAsync(businessId);
        var order = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            BusinessId = businessId,
            TransactionCode = code,
            TransactionDate = DateTime.UtcNow,
            Status = "Draft",
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        await _transactions.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();
        return order.TransactionId;
    }

    public async Task<OrderDetailResponse> GetOrderDetailAsync(Guid transactionId)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null)
        {
            throw new NotFoundException("Order not found.");
        }

        Invoice? invoice = null;
        if (!string.IsNullOrEmpty(order.InvoiceId))
        {
            invoice = await _invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == order.InvoiceId);
        }

        int? quotaRemaining = null;
        int? quotaWarningThreshold = null;

        var eInvoiceConfig = await _eInvoiceConfigs.FirstOrDefaultAsync(c => c.BusinessId == order.BusinessId && c.IsEnabled);
        if (eInvoiceConfig != null)
        {
            quotaWarningThreshold = eInvoiceConfig.QuotaWarningThreshold;
            quotaRemaining = await _eInvoiceService.GetQuotaRemainingAsync(eInvoiceConfig);
        }

        return new OrderDetailResponse
        {
            TransactionId = order.TransactionId,
            TransactionCode = order.TransactionCode,
            TransactionDate = order.TransactionDate,
            Status = order.Status,
            Note = order.Note,
            InvoiceNumber = order.InvoiceId,
            TaxAuthorityCode = invoice?.TaxAuthorityCode,
            OfficialPdfUrl = invoice?.OfficialPdfUrl,
            OfficialXmlUrl = invoice?.OfficialXmlUrl,
            InvoiceStatus = invoice?.Status,
            SePayMessage = invoice?.SePayMessage,
            QuotaRemaining = quotaRemaining,
            QuotaWarningThreshold = quotaWarningThreshold,
            SubTotal = order.SubTotal,
            DiscountType = order.DiscountType,
            DiscountValue = order.DiscountValue,
            DiscountAmount = order.DiscountAmount,
            SurchargeName = order.SurchargeName,
            SurchargeType = order.SurchargeType,
            SurchargeValue = order.SurchargeValue,
            SurchargeAmount = order.SurchargeAmount,
            TotalAmount = order.TotalAmount,
            Items = order.TransactionItems.Select(x => new OrderItemResponse
            {
                TransactionItemId = x.TransactionItemId,
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                Unit = x.Unit,
                UnitPrice = x.UnitPrice,
                Quantity = x.Quantity,
                DiscountType = x.DiscountType,
                DiscountValue = x.DiscountValue,
                DiscountAmount = x.DiscountAmount,
                LineTotal = x.LineTotal,
                Note = x.Note
            }).ToList(),
            Payments = order.Payments.Select(x => new OrderPaymentResponse
            {
                PaymentId = x.PaymentId,
                PaymentMethod = x.PaymentMethod,
                Amount = x.Amount,
                PaymentAccountId = x.PaymentAccountId,
                BankName = x.PaymentAccount?.BankName,
                PaidAt = x.PaidAt
            }).ToList()
        };
    }

    public async Task<PagedResult<OrderSummaryResponse>> GetOrdersByBusinessAsync(
        Guid businessId,
        int page,
        int pageSize,
        string? status = null,
        string? paymentMethod = null,
        decimal? minAmount = null,
        decimal? maxAmount = null)
    {
        var count = await _transactions.CountByBusinessIdAsync(businessId, status, paymentMethod, minAmount, maxAmount);
        var transactions = await _transactions.GetByBusinessIdAsync(businessId, page, pageSize, status, paymentMethod, minAmount, maxAmount);

        var items = transactions.Select(x => new OrderSummaryResponse
        {
            TransactionId = x.TransactionId,
            TransactionCode = x.TransactionCode,
            TransactionDate = x.TransactionDate,
            TotalAmount = x.TotalAmount,
            Status = x.Status,
            ItemCount = x.TransactionItems.Count,
            InvoiceNumber = x.InvoiceId
        }).ToList();

        return new PagedResult<OrderSummaryResponse>
        {
            Items = items,
            TotalCount = count,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task AddItemAsync(Guid transactionId, AddOrderItemRequest request)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new NotFoundException("Order not found.");
        if (order.Status != "Draft") throw new ConflictException("Cannot modify items of a non-draft order.");
        if (request.Quantity <= 0) throw new BadRequestException("Quantity must be greater than zero.");

        var product = await _products.FirstOrDefaultAsync(p => p.Id == request.ProductId);
        if (product == null || product.BusinessId != order.BusinessId)
            throw new NotFoundException("Product not found or does not belong to this business.");

        var prices = await _productPrices.FindAsync(x => x.ProductId == request.ProductId);
        var now = DateTime.UtcNow;
        var unitPrice = prices
            .Where(p => p.ApplyDate <= now)
            .OrderByDescending(p => p.ApplyDate)
            .Select(p => p.Price)
            .FirstOrDefault();
        if (unitPrice == 0 && prices.Any())
        {
            unitPrice = prices.OrderBy(p => p.ApplyDate).Select(p => p.Price).First();
        }

        // Tính UnitCost từ BOM (nguyên liệu) hoặc Product.CostPrice
        decimal unitCost = 0;
        var pIngredients = await _productIngredients.GetByProductIdAsync(request.ProductId);
        if (pIngredients != null && pIngredients.Any())
        {
            foreach (var pi in pIngredients)
            {
                var ingredient = await _ingredients.GetByIdAsync(pi.IngredientId);
                if (ingredient?.EstimatedPrice.HasValue == true)
                {
                    unitCost += ingredient.EstimatedPrice.Value * pi.Quantity;
                }
            }
        }
        else if (product.CostPrice.HasValue)
        {
            unitCost = product.CostPrice.Value;
        }

        var roundedUnitCost = Math.Round(unitCost, 6, MidpointRounding.AwayFromZero);

        var existing = order.TransactionItems.FirstOrDefault(x => x.ProductId == request.ProductId && x.Note == request.Note);
        if (existing != null)
        {
            existing.Quantity += request.Quantity;
            existing.UnitCost = roundedUnitCost;
            existing.CostAmount = Math.Round(roundedUnitCost * existing.Quantity, 2, MidpointRounding.AwayFromZero);
            if (!string.IsNullOrEmpty(request.DiscountType))
            {
                existing.DiscountType = request.DiscountType;
                existing.DiscountValue = request.DiscountValue;
            }
        }
        else
        {
            var item = new TransactionItem
            {
                TransactionItemId = Guid.NewGuid(),
                TransactionId = order.TransactionId,
                ProductId = request.ProductId,
                ProductName = product.Name,
                Unit = product.Unit,
                UnitPrice = unitPrice,
                Quantity = request.Quantity,
                DiscountType = request.DiscountType,
                DiscountValue = request.DiscountValue,
                Note = request.Note,
                UnitCost = roundedUnitCost,
                CostAmount = Math.Round(roundedUnitCost * request.Quantity, 2, MidpointRounding.AwayFromZero),
                CreatedAt = DateTime.UtcNow
            };
            await _transactionItems.AddAsync(item);
            // KHÔNG gọi order.TransactionItems.Add(item) ở đây vì EF Core Navigation Fixup
            // đã tự động thêm item vào collection sau khi AddAsync được gọi.
            // Nếu gọi thêm Add() thủ công, item sẽ xuất hiện 2 lần trong danh sách in-memory
            // và RecalculateOrder sẽ tính tổng tiền gấp đôi!
        }

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateItemAsync(Guid transactionId, Guid itemId, UpdateOrderItemRequest request)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new Exception("Order not found.");
        if (order.Status != "Draft") throw new Exception("Cannot modify items of a non-draft order.");

        var item = order.TransactionItems.FirstOrDefault(x => x.TransactionItemId == itemId);
        if (item == null) throw new Exception("Order item not found.");

        if (request.Quantity.HasValue)
        {
            if (request.Quantity.Value <= 0)
            {
                order.TransactionItems.Remove(item);
            }
            else
            {
                item.Quantity = request.Quantity.Value;
                item.CostAmount = Math.Round(item.UnitCost * item.Quantity, 2, MidpointRounding.AwayFromZero);
            }
        }

        if (request.DiscountType != null)
        {
            item.DiscountType = string.IsNullOrEmpty(request.DiscountType) ? null : request.DiscountType;
            item.DiscountValue = request.DiscountValue;
        }

        if (request.Note != null)
        {
            item.Note = string.IsNullOrEmpty(request.Note) ? null : request.Note;
        }

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveItemAsync(Guid transactionId, Guid itemId)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new Exception("Order not found.");
        if (order.Status != "Draft") throw new Exception("Cannot modify items of a non-draft order.");

        var item = order.TransactionItems.FirstOrDefault(x => x.TransactionItemId == itemId);
        if (item == null) throw new Exception("Order item not found.");

        order.TransactionItems.Remove(item);
        
        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    /*
    public async Task ApplyDiscountAsync(Guid transactionId, ApplyDiscountRequest request)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new NotFoundException("Order not found.");
        if (order.Status != "Draft") throw new ConflictException("Cannot modify discount of a non-draft order.");

        order.DiscountType = request.DiscountType;
        order.DiscountValue = request.DiscountValue;

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveDiscountAsync(Guid transactionId)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new NotFoundException("Order not found.");
        if (order.Status != "Draft") throw new ConflictException("Cannot modify discount of a non-draft order.");

        order.DiscountType = null;
        order.DiscountValue = null;
        order.DiscountAmount = 0;

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ApplySurchargeAsync(Guid transactionId, ApplySurchargeRequest request)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new NotFoundException("Order not found.");
        if (order.Status != "Draft") throw new ConflictException("Cannot modify surcharge of a non-draft order.");

        order.SurchargeName = request.SurchargeName;
        order.SurchargeType = request.SurchargeType;
        order.SurchargeValue = request.SurchargeValue;

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveSurchargeAsync(Guid transactionId)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null) throw new NotFoundException("Order not found.");
        if (order.Status != "Draft") throw new ConflictException("Cannot modify surcharge of a non-draft order.");

        order.SurchargeName = null;
        order.SurchargeType = null;
        order.SurchargeValue = null;
        order.SurchargeAmount = 0;

        RecalculateOrder(order);
        await _unitOfWork.SaveChangesAsync();
    }
    */

    public async Task<InvoiceDetailResponse> CheckoutAsync(Guid transactionId, CheckoutRequest request)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null)
        {
            throw new NotFoundException("Order not found.");
        }

        if (order.Status != "Draft")
        {
            throw new ConflictException($"Cannot checkout order with status '{order.Status}'. Only 'Draft' orders can be checked out.");
        }

        if (!order.TransactionItems.Any())
        {
            throw new BadRequestException("Cannot checkout an empty order. Please add at least one product.");
        }

        if (order.TransactionItems.Any(x => x.Quantity <= 0))
        {
            throw new BadRequestException("Cannot checkout an order containing a non-positive item quantity.");
        }

        var totalPaidAmount = request.Payments.Sum(x => x.Amount);
        if (Math.Round(totalPaidAmount, 2) < Math.Round(order.TotalAmount, 2))
        {
            throw new BadRequestException($"Paid amount ({totalPaidAmount}) is less than total order amount ({order.TotalAmount}).");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var paidAt = DateTime.UtcNow;

            // Kiểm tra xem BankTransfer có dùng tài khoản SePay (có webhook tự động) không.
            // Nếu tài khoản là static VietQR (không có SePayBankAccountXid), đơn sẽ được Complete ngay.
            bool isAwaitingPayment = false;
            foreach (var paymentEntry in request.Payments)
            {
                if (paymentEntry.PaymentMethod.Equals("Transfer", StringComparison.OrdinalIgnoreCase)
                    && paymentEntry.PaymentAccountId.HasValue)
                {
                    var account = await _paymentAccounts.GetByIdAsync(paymentEntry.PaymentAccountId.Value);
                    if (account == null || account.BusinessId != order.BusinessId)
                        throw new NotFoundException($"Payment account '{paymentEntry.PaymentAccountId}' not found or does not belong to this business.");

                    // Chỉ AwaitingPayment nếu tài khoản có liên kết SePay
                    if (!string.IsNullOrEmpty(account.SePayBankAccountXid))
                        isAwaitingPayment = true;
                }
            }

            if (isAwaitingPayment)
            {
                var transitioned = await _transactions.TryTransitionStatusAsync(
                    order.TransactionId,
                    TransactionStatus.Draft,
                    TransactionStatus.AwaitingPayment);
                if (!transitioned)
                {
                    throw new ConflictException("Order status changed while checkout was being processed.");
                }

                order.Status = TransactionStatus.AwaitingPayment;
            }
            else
            {
                await CompleteOrderAsync(order, TransactionStatus.Draft);
            }

            foreach (var paymentEntry in request.Payments)
            {
                var isBankTransfer = paymentEntry.PaymentMethod.Equals("Transfer", StringComparison.OrdinalIgnoreCase);
                var payment = new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    TransactionId = order.TransactionId,
                    PaymentMethod = paymentEntry.PaymentMethod,
                    Amount = paymentEntry.Amount,
                    PaymentAccountId = paymentEntry.PaymentAccountId,
                    PaidAt = (isAwaitingPayment && isBankTransfer) ? null : paidAt,
                    CreatedAt = paidAt
                };

                await _payments.AddAsync(payment);
                order.Payments.Add(payment);
            }

            await _unitOfWork.SaveChangesAsync();

            var invoiceNumber = await _invoiceService.GenerateFromOrderAsync(order.TransactionId);

            var invoice = await _invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);
            if (invoice != null)
            {
                invoice.BuyerTaxCode = request.BuyerTaxCode;
                invoice.BuyerCompanyName = request.BuyerCompanyName;
                invoice.BuyerAddress = request.BuyerAddress;
                invoice.BuyerEmail = request.BuyerEmail;
                await _unitOfWork.SaveChangesAsync();
            }

            // Tự động phát hành HĐĐT nếu đơn đã hoàn thành và shop có thiết lập
            if (order.Status == TransactionStatus.Completed)
            {
                var business = await _businessProfiles.GetByIdAsync(order.BusinessId);
                if (business != null && business.PreferElectronicInvoice)
                {
                    var eInvoiceConfig = await _eInvoiceConfigs.FirstOrDefaultAsync(c => c.BusinessId == order.BusinessId && c.IsEnabled);
                    if (eInvoiceConfig != null)
                    {
                        if (invoice != null)
                        {
                            try
                            {
                                // Load details including related products for unit info mapping
                                invoice = await _invoices.GetByNumberWithDetailsAsync(invoiceNumber);
                                if (invoice != null)
                                {
                                    invoice.Business = business;

                                    invoice.Status = InvoiceStatus.Processing;
                                    await _unitOfWork.SaveChangesAsync();

                                    var eInvoiceResult = await _eInvoiceService.IssueInvoiceAsync(invoice, eInvoiceConfig);
                                    
                                    invoice.SePayTrackingCode = eInvoiceResult.TrackingCode;
                                    invoice.SePayReferenceCode = eInvoiceResult.ReferenceCode;
                                    invoice.SePayMessage = eInvoiceResult.ErrorMessage;

                                    if (eInvoiceResult.Success)
                                    {
                                        invoice.TaxAuthorityCode = eInvoiceResult.TaxAuthorityCode;
                                        invoice.OfficialPdfUrl = eInvoiceResult.OfficialPdfUrl;
                                        invoice.OfficialXmlUrl = eInvoiceResult.OfficialXmlUrl;
                                        invoice.Status = InvoiceStatus.Issued;
                                    }
                                    else
                                    {
                                        invoice.Status = InvoiceStatus.Failed;
                                    }
                                    await _unitOfWork.SaveChangesAsync();
                                }
                            }
                            catch (Exception ex)
                            {
                                if (invoice != null)
                                {
                                    invoice.Status = InvoiceStatus.Failed;
                                    invoice.SePayMessage = $"Lỗi xử lý hệ thống: {ex.Message}";
                                    await _unitOfWork.SaveChangesAsync();
                                }
                            }
                        }
                    }
                }
            }

            await _unitOfWork.CommitTransactionAsync();

            return await _invoiceService.GetInvoiceDetailAsync(invoiceNumber);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task CancelOrderAsync(Guid transactionId)
    {
        var order = await _transactions.GetByIdAsync(transactionId);
        if (order == null)
        {
            throw new NotFoundException("Order not found.");
        }

        if (order.Status != "Draft" && order.Status != "AwaitingPayment")
        {
            throw new ConflictException($"Cannot cancel order with status '{order.Status}'. Only 'Draft' or 'AwaitingPayment' orders can be cancelled.");
        }

        order.Status = "Cancelled";
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<InvoiceDetailResponse> ConfirmPaymentAsync(Guid transactionId)
    {
        var order = await _transactions.GetByIdWithDetailsAsync(transactionId);
        if (order == null)
        {
            throw new NotFoundException("Order not found.");
        }

        if (order.Status != TransactionStatus.AwaitingPayment)
        {
            throw new ConflictException($"Cannot confirm payment for order with status '{order.Status}'. Only 'AwaitingPayment' orders can be confirmed.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await CompleteOrderAsync(order, TransactionStatus.AwaitingPayment);

            var paidAt = DateTime.UtcNow;

            foreach (var payment in order.Payments)
            {
                if (payment.PaidAt == null)
                {
                    payment.PaidAt = paidAt;
                }
            }

            await _unitOfWork.SaveChangesAsync();

            Invoice? invoice = null;
            if (!string.IsNullOrEmpty(order.InvoiceId))
            {
                invoice = await _invoices.FirstOrDefaultAsync(i => i.InvoiceNumber == order.InvoiceId);
            }

            if (invoice != null)
            {
                var business = await _businessProfiles.GetByIdAsync(order.BusinessId);
                if (business != null && business.PreferElectronicInvoice)
                {
                    var eInvoiceConfig = await _eInvoiceConfigs.FirstOrDefaultAsync(c => c.BusinessId == order.BusinessId && c.IsEnabled);
                    if (eInvoiceConfig != null)
                    {
                        try
                        {
                            // Load details including related products for unit info mapping
                            invoice = await _invoices.GetByNumberWithDetailsAsync(invoice.InvoiceNumber);
                            if (invoice != null)
                            {
                                invoice.Business = business;

                                invoice.Status = InvoiceStatus.Processing;
                                await _unitOfWork.SaveChangesAsync();

                                var eInvoiceResult = await _eInvoiceService.IssueInvoiceAsync(invoice, eInvoiceConfig);
                                
                                invoice.SePayTrackingCode = eInvoiceResult.TrackingCode;
                                invoice.SePayReferenceCode = eInvoiceResult.ReferenceCode;
                                invoice.SePayMessage = eInvoiceResult.ErrorMessage;

                                if (eInvoiceResult.Success)
                                {
                                    invoice.TaxAuthorityCode = eInvoiceResult.TaxAuthorityCode;
                                    invoice.OfficialPdfUrl = eInvoiceResult.OfficialPdfUrl;
                                    invoice.OfficialXmlUrl = eInvoiceResult.OfficialXmlUrl;
                                    invoice.Status = InvoiceStatus.Issued;
                                }
                                else
                                {
                                    invoice.Status = InvoiceStatus.Failed;
                                }
                                await _unitOfWork.SaveChangesAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            if (invoice != null)
                            {
                                invoice.Status = InvoiceStatus.Failed;
                                invoice.SePayMessage = $"Lỗi xử lý hệ thống: {ex.Message}";
                                await _unitOfWork.SaveChangesAsync();
                            }
                        }
                    }
                    else
                    {
                        invoice.Status = InvoiceStatus.Issued;
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
                else
                {
                    invoice.Status = InvoiceStatus.Issued;
                    await _unitOfWork.SaveChangesAsync();
                }
            }

            await _unitOfWork.CommitTransactionAsync();

            return await _invoiceService.GetInvoiceDetailAsync(order.InvoiceId!);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task CompleteOrderAsync(Transaction order, string expectedStatus)
    {
        var transitioned = await _transactions.TryTransitionStatusAsync(
            order.TransactionId,
            expectedStatus,
            TransactionStatus.Completed);
        if (!transitioned)
        {
            throw new ConflictException("Order status changed while completion was being processed.");
        }

        order.Status = TransactionStatus.Completed;
        await DeductInventoryForCompletedOrderAsync(order);
    }

    private async Task DeductInventoryForCompletedOrderAsync(Transaction order)
    {
        var soldQuantitiesByProduct = order.TransactionItems
            .Where(x => x.ProductId.HasValue && x.Quantity > 0)
            .GroupBy(x => x.ProductId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Quantity));

        var productDeltas = new Dictionary<Guid, decimal>();
        var ingredientDeltas = new Dictionary<Guid, decimal>();

        foreach (var (productId, soldQuantity) in soldQuantitiesByProduct.OrderBy(x => x.Key))
        {
            var bom = await _productIngredients.GetByProductIdAsync(productId);
            if (bom.Count == 0)
            {
                productDeltas[productId] = soldQuantity;
                continue;
            }

            foreach (var bomItem in bom)
            {
                var delta = bomItem.Quantity * soldQuantity;
                ingredientDeltas[bomItem.IngredientId] =
                    ingredientDeltas.GetValueOrDefault(bomItem.IngredientId) + delta;
            }
        }

        foreach (var (productId, quantity) in productDeltas.OrderBy(x => x.Key))
        {
            await _products.DecrementStockAsync(productId, quantity);
        }

        foreach (var (ingredientId, quantity) in ingredientDeltas.OrderBy(x => x.Key))
        {
            await _ingredients.DecrementStockAsync(ingredientId, quantity);
        }
    }

    private void RecalculateOrder(Transaction order)
    {
        decimal subTotal = 0;
        foreach (var item in order.TransactionItems)
        {
            decimal itemDiscount = 0;
            if (item.DiscountType == "Percentage" && item.DiscountValue.HasValue)
            {
                itemDiscount = (item.UnitPrice * item.Quantity) * (item.DiscountValue.Value / 100);
            }
            else if (item.DiscountType == "Fixed" && item.DiscountValue.HasValue)
            {
                itemDiscount = item.DiscountValue.Value;
            }
            
            item.DiscountAmount = Math.Max(0, Math.Min(item.UnitPrice * item.Quantity, itemDiscount));
            item.LineTotal = Math.Max(0, (item.UnitPrice * item.Quantity) - item.DiscountAmount);
            subTotal += item.LineTotal;
        }

        order.SubTotal = subTotal;

        // Force order-level discount and surcharge to 0 (unsupported feature)
        order.DiscountAmount = 0;
        order.SurchargeAmount = 0;

        order.TotalAmount = order.SubTotal;
    }
}
