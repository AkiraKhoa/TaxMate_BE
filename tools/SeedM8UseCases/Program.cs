using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;

// Intentionally data-only: no DROP, ALTER TABLE, EnsureCreated, Migrate, or raw SQL.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var root = new DirectoryInfo(AppContext.BaseDirectory);
while (root is not null && !File.Exists(Path.Combine(root.FullName, "TaxMate.sln"))) root = root.Parent;
if (root is null) throw new InvalidOperationException("TaxMate.sln was not found.");

var configuration = new ConfigurationBuilder()
    .SetBasePath(Path.Combine(root.FullName, "src", "TaxMate.API"))
    .AddJsonFile("appsettings.json", optional: false)
    .Build();
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
    .Options;

await using var db = new AppDbContext(options);
const string markerEmail = "m8-usecases@taxmate.local";
if (await db.Users.AnyAsync(x => x.Email == markerEmail))
{
    Console.WriteLine("M8 use-case seed already exists; no rows were changed.");
    return;
}

var fnb = await db.BusinessCategories.SingleAsync(x => x.Code == "FNB");
var service = await db.BusinessCategories.SingleAsync(x => x.Code == "SERVICE_CONSTRUCT");
var now = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc);
var payableOwnerId = Guid.Parse("90000000-0000-4000-8000-000000000001");
var refundOwnerId = Guid.Parse("90000000-0000-4000-8000-000000000002");
var inventoryOwnerId = Guid.Parse("90000000-0000-4000-8000-000000000003");
var payableBusinessId = Guid.Parse("90000000-0000-4000-8000-000000000101");
var refundBusinessId = Guid.Parse("90000000-0000-4000-8000-000000000102");
var inventoryOnBusinessId = Guid.Parse("90000000-0000-4000-8000-000000000103");
var inventoryOffBusinessId = Guid.Parse("90000000-0000-4000-8000-000000000104");

User Owner(Guid id, string email, string taxCode, string name) => new()
{
    Id = id, Email = email, TaxCode = taxCode, PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123456", 12), FullName = name, Role = UserRoles.Owner,
    AccountStatus = AccountStatus.Active, PersonalIncomeTaxMethod = PersonalIncomeTaxMethods.IncomeBased,
    TaxMethodEffectiveYear = 2026, TaxProfileConfirmedAt = now, CreatedAt = now, UpdatedAt = now
};
db.Users.AddRange(
    Owner(payableOwnerId, markerEmail, "M8TAX0001", "M8 QTT phải nộp"),
    Owner(refundOwnerId, "m8-qtt-overpaid@taxmate.local", "M8TAX0002", "M8 QTT nộp thừa"),
    Owner(inventoryOwnerId, "m8-inventory@taxmate.local", "M8TAX0003", "M8 Kho"));

BusinessProfile Business(Guid id, Guid ownerId, string name, Guid categoryId, bool stockEnabled) => new()
{
    Id = id, OwnerId = ownerId, BusinessName = name, MainCategoryId = categoryId,
    Address = "Dữ liệu test M8", IsStockTrackingEnabled = stockEnabled,
    InventoryInitializedAt = stockEnabled ? now.AddMonths(-6) : now.AddMonths(-3),
    TaxAuthorityLevel = TaxAuthorityLevels.Local, TaxAdministrationAreaCode = "TEST-M8",
    ManagingTaxAuthority = "Thuế cơ sở test", CollectingAuthority = "Kho bạc test",
    BusinessLocationCode = "M8-LOC", CreatedAt = now, UpdatedAt = now
};

db.BusinessProfiles.AddRange(
    Business(payableBusinessId, payableOwnerId, "M8 QTT - Còn phải nộp", service.BusinessCategoryId, false),
    Business(refundBusinessId, refundOwnerId, "M8 QTT - Nộp thừa / hoàn / bù trừ", service.BusinessCategoryId, false),
    Business(inventoryOnBusinessId, inventoryOwnerId, "M8 Kho đang bật", fnb.BusinessCategoryId, true),
    Business(inventoryOffBusinessId, inventoryOwnerId, "M8 Kho đã tắt", fnb.BusinessCategoryId, false));

TaxPeriod Quarter(Guid businessId, int quarter) => new()
{
    Id = Guid.NewGuid(), BusinessId = businessId, PeriodType = TaxPeriodTypes.Quarterly, Year = 2026,
    Quarter = quarter, PeriodStartDate = new DateTime(2026, (quarter - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc),
    PeriodEndDate = quarter == 4 ? new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc) : new DateTime(2026, quarter * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc),
    Status = TaxPeriodStatuses.Open, EvidenceReviewedAt = now, EvidenceReviewedByUserId = businessId == payableBusinessId ? payableOwnerId : refundOwnerId, CreatedAt = now, UpdatedAt = now
};
var payableQ3 = Quarter(payableBusinessId, 3);
var refundQ3 = Quarter(refundBusinessId, 3);
var payableQ4 = Quarter(payableBusinessId, 4);
var refundQ4 = Quarter(refundBusinessId, 4);
db.TaxPeriods.AddRange(payableQ3, refundQ3, payableQ4, refundQ4);
db.TaxPayments.AddRange(
    new TaxPayment { Id = Guid.NewGuid(), TaxPeriodId = payableQ4.Id, TaxType = TaxTypes.PersonalIncomeTax, PaymentCode = "M8-PIT-PAID-01", Amount = 20_000_000m, PaymentDate = now, PaymentMethod = "Bank", Status = TaxPaymentStatuses.Completed, CreatedAt = now, UpdatedAt = now },
    new TaxPayment { Id = Guid.NewGuid(), TaxPeriodId = refundQ4.Id, TaxType = TaxTypes.PersonalIncomeTax, PaymentCode = "M8-PIT-OVER-01", Amount = 90_000_000m, PaymentDate = now, PaymentMethod = "Bank", Status = TaxPaymentStatuses.Completed, CreatedAt = now, UpdatedAt = now });

var expenseCategory = new ExpenseCategory { ExpenseCategoryId = Guid.NewGuid(), CategoryName = "M8 chi phí được trừ", S2cGroupCode = S2cGroupCodes.PurchasedServices, IsDefault = false, CreatedAt = now, UpdatedAt = now };
db.ExpenseCategories.Add(expenseCategory);
Transaction Sale(Guid businessId, string code, string invoiceNumber, decimal amount) => new()
{
    TransactionId = Guid.NewGuid(), BusinessId = businessId, TransactionCode = code, TransactionDate = now.AddMonths(-1),
    CompletedAt = now.AddMonths(-1), SubTotal = amount, TotalAmount = amount, InvoiceId = invoiceNumber, Status = TransactionStatus.Completed,
    TransactionType = TransactionTypes.Sale, Note = "Nguồn doanh thu S2b cho QTT", CreatedAt = now, UpdatedAt = now
};
var payableSale = Sale(payableBusinessId, "M8-S2B-PAYABLE", "M8-IV-PAYABLE", 1_600_000_000m);
var refundSale = Sale(refundBusinessId, "M8-S2B-OVERPAID", "M8-IV-OVERPAID", 1_400_000_000m);
db.Transactions.AddRange(payableSale, refundSale);
db.Invoices.AddRange(
    new Invoice { InvoiceNumber = "M8-IV-PAYABLE", BusinessId = payableBusinessId, TotalAmount = 1_600_000_000m, IssueDate = now.AddMonths(-1), Status = "Issued", CreatedAt = now, UpdatedAt = now },
    new Invoice { InvoiceNumber = "M8-IV-OVERPAID", BusinessId = refundBusinessId, TotalAmount = 1_400_000_000m, IssueDate = now.AddMonths(-1), Status = "Issued", CreatedAt = now, UpdatedAt = now });
db.Expenses.AddRange(
    new Expense { ExpenseId = Guid.NewGuid(), BusinessId = payableBusinessId, ExpenseCategoryId = expenseCategory.ExpenseCategoryId, VoucherNumber = "M8-S2C-PAYABLE", ExpenseTitle = "Chi phí nguồn QTT", Amount = 800_000_000m, ExpenseDate = now.AddMonths(-1), PaymentMethod = "Bank", FileUrl = "seed://evidence", CreatedAt = now, UpdatedAt = now },
    new Expense { ExpenseId = Guid.NewGuid(), BusinessId = refundBusinessId, ExpenseCategoryId = expenseCategory.ExpenseCategoryId, VoucherNumber = "M8-S2C-OVERPAID", ExpenseTitle = "Chi phí nguồn QTT", Amount = 1_000_000_000m, ExpenseDate = now.AddMonths(-1), PaymentMethod = "Bank", FileUrl = "seed://evidence", CreatedAt = now, UpdatedAt = now });

var inventoryProductId = Guid.Parse("90000000-0000-4000-8000-000000000201");
var offInventoryProductId = Guid.Parse("90000000-0000-4000-8000-000000000202");
db.Products.AddRange(
    new Product { Id = inventoryProductId, BusinessId = inventoryOnBusinessId, BusinessCategoryId = fnb.BusinessCategoryId, ProductCode = "M8-STOCK-ON", Name = "Hàng kho đang bật", Unit = "cái", CostPrice = 100_000m, StockQuantity = 70m, Status = ProductStatus.Active, CreatedAt = now, UpdatedAt = now },
    new Product { Id = offInventoryProductId, BusinessId = inventoryOffBusinessId, BusinessCategoryId = fnb.BusinessCategoryId, ProductCode = "M8-STOCK-OFF", Name = "Hàng kho đã tắt", Unit = "cái", CostPrice = 100_000m, StockQuantity = 70m, Status = ProductStatus.Active, CreatedAt = now, UpdatedAt = now });

InventoryMovement Move(Guid businessId, Guid productId, string type, decimal quantity, decimal value, string document, DateTime at) => new()
{
    InventoryMovementId = Guid.NewGuid(), BusinessId = businessId, ProductId = productId, MovementType = type,
    Quantity = quantity, TotalValue = value, OccurredAt = at, DocumentNumber = document,
    Description = "M8 inventory seed: " + type, CreatedAt = now, UpdatedAt = now
};
var onPurchaseExpenseId = Guid.NewGuid();
var offPurchaseExpenseId = Guid.NewGuid();
var onSale = Sale(inventoryOnBusinessId, "M8-ORDER-ON", "M8-IV-ORDER-ON", 2_000_000m);
var offSale = Sale(inventoryOffBusinessId, "M8-ORDER-OFF", "M8-IV-ORDER-OFF", 2_000_000m);
db.Transactions.AddRange(onSale, offSale);
db.Invoices.AddRange(
    new Invoice { InvoiceNumber = "M8-IV-ORDER-ON", BusinessId = inventoryOnBusinessId, TotalAmount = 2_000_000m, IssueDate = now.AddDays(-7), Status = "Issued", CreatedAt = now, UpdatedAt = now },
    new Invoice { InvoiceNumber = "M8-IV-ORDER-OFF", BusinessId = inventoryOffBusinessId, TotalAmount = 2_000_000m, IssueDate = now.AddDays(-7), Status = "Issued", CreatedAt = now, UpdatedAt = now });
db.TransactionItems.AddRange(
    new TransactionItem { TransactionItemId = Guid.NewGuid(), TransactionId = onSale.TransactionId, ProductId = inventoryProductId, ProductName = "Hàng kho đang bật", Unit = "cái", UnitPrice = 100_000m, Quantity = 20m, LineTotal = 2_000_000m, UnitCost = 100_000m, CostAmount = 2_000_000m, CreatedAt = now, UpdatedAt = now },
    new TransactionItem { TransactionItemId = Guid.NewGuid(), TransactionId = offSale.TransactionId, ProductId = offInventoryProductId, ProductName = "Hàng kho đã tắt", Unit = "cái", UnitPrice = 100_000m, Quantity = 20m, LineTotal = 2_000_000m, UnitCost = 100_000m, CostAmount = 2_000_000m, CreatedAt = now, UpdatedAt = now });
db.Expenses.AddRange(
    new Expense { ExpenseId = onPurchaseExpenseId, BusinessId = inventoryOnBusinessId, ExpenseCategoryId = expenseCategory.ExpenseCategoryId, VoucherNumber = "M8-PURCHASE-ON", ExpenseTitle = "Phiếu nhập kho đang bật", Amount = 4_000_000m, ExpenseDate = now.AddMonths(-2), PaymentMethod = "Bank", FileUrl = "seed://purchase-on", CreatedAt = now, UpdatedAt = now },
    new Expense { ExpenseId = offPurchaseExpenseId, BusinessId = inventoryOffBusinessId, ExpenseCategoryId = expenseCategory.ExpenseCategoryId, VoucherNumber = "M8-PURCHASE-OFF", ExpenseTitle = "Phiếu nhập kho đã tắt", Amount = 4_000_000m, ExpenseDate = now.AddMonths(-2), PaymentMethod = "Bank", FileUrl = "seed://purchase-off", CreatedAt = now, UpdatedAt = now });
InventoryMovement SourceMove(Guid businessId, Guid productId, string type, decimal quantity, decimal? value, string document, DateTime at, Guid referenceId) => new()
{
    InventoryMovementId = Guid.NewGuid(), BusinessId = businessId, ProductId = productId, MovementType = type,
    Quantity = quantity, TotalValue = value, OccurredAt = at, DocumentNumber = document, ReferenceId = referenceId,
    Description = "M8 inventory seed: " + type, CreatedAt = now, UpdatedAt = now
};
db.InventoryMovements.AddRange(
    Move(inventoryOnBusinessId, inventoryProductId, InventoryMovementTypes.OpeningBalance, 50m, 5_000_000m, "M8-OPEN-ON", now.AddMonths(-6)),
    SourceMove(inventoryOnBusinessId, inventoryProductId, InventoryMovementTypes.PurchaseIn, 40m, 4_000_000m, "M8-IN-ON", now.AddMonths(-2), onPurchaseExpenseId),
    SourceMove(inventoryOnBusinessId, inventoryProductId, InventoryMovementTypes.OrderOut, 20m, null, "M8-OUT-ON", now.AddDays(-7), onSale.TransactionId),
    Move(inventoryOffBusinessId, offInventoryProductId, InventoryMovementTypes.OpeningBalance, 50m, 5_000_000m, "M8-OPEN-OFF", now.AddMonths(-6)),
    SourceMove(inventoryOffBusinessId, offInventoryProductId, InventoryMovementTypes.PurchaseIn, 40m, 4_000_000m, "M8-IN-OFF", now.AddMonths(-2), offPurchaseExpenseId),
    SourceMove(inventoryOffBusinessId, offInventoryProductId, InventoryMovementTypes.OrderOut, 20m, null, "M8-OUT-OFF", now.AddDays(-7), offSale.TransactionId));

await db.SaveChangesAsync();
Console.WriteLine("Inserted M8 use-case data only. No schema operation was executed.");
