using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TaxMate.Model.Common;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;

namespace PosDataSeeder;

class Program
{
    static async Task Main(string[] args)
    {
        // Enable Npgsql Legacy Timestamp Behavior for EF Core Migrations compatibility
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        Console.WriteLine("=================================================");
        Console.WriteLine("      TAXMATE POS TEST DATA SEEDER TOOL          ");
        Console.WriteLine("=================================================");

        var connString = "Host=localhost;Port=5432;Database=taxmate_db;Username=postgres;Password=12345";
        
        bool scaleToBillion = args.Contains("--scale-billion");
        bool resetDb = args.Contains("--reset") || true;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connString);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

        using var db = new AppDbContext(optionsBuilder.Options);

        try
        {
            if (resetDb)
            {
                Console.WriteLine("[0/5] Dropping existing database and running EF Core Migrations...");
                await db.Database.EnsureDeletedAsync();
                Console.WriteLine("      Database dropped.");
                await db.Database.MigrateAsync();
                Console.WriteLine("      EF Core Migrations applied successfully!");
            }
            else
            {
                Console.WriteLine("[1/5] Checking Database Connection...");
                await db.Database.EnsureCreatedAsync();
                Console.WriteLine("      Database connection OK.");
            }

            // 1. Level 2 - User specified by Team ("Nguyen Truong Giang")
            Console.WriteLine("[2/5] Seeding User & BusinessProfile...");
            var userId = Guid.Parse("e03ad3be-ea8e-41a2-9348-88ce58ac2b56");
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId || u.Email == "giangnguyen102004@gmail.com");
            if (user == null)
            {
                user = new User
                {
                    Id = userId,
                    Email = "giangnguyen102004@gmail.com",
                    TaxCode = "079204022790",
                    PasswordHash = "$2a$12$pJzsQz2RkJAcUaL3J/ypeOLQj4b8Q18aS2vuiPuCsM95a1oEGz11W",
                    FullName = "Nguyen Truong Giang",
                    Phone = "0909910224",
                    Role = "Owner",
                    AccountStatus = AccountStatus.Active,
                    EmailVerificationToken = "T_FTSLCV5u5Xn-aaI7m4irAyigXAXCUAeqSGIvugze0",
                    EmailVerificationTokenExpiresAt = DateTime.Parse("2026-07-30 12:16:09.787328"),
                    CreatedAt = DateTime.Parse("2026-07-29 12:16:10.598651"),
                    UpdatedAt = DateTime.Parse("2026-07-29 12:16:10.598652")
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
                Console.WriteLine($"      Created User: {user.FullName} ({user.Email}) [Id: {user.Id}]");
            }

            var business = await db.BusinessProfiles.FirstOrDefaultAsync(b => b.OwnerId == user.Id);
            if (business == null)
            {
                business = new BusinessProfile
                {
                    Id = Guid.NewGuid(),
                    OwnerId = user.Id,
                    BusinessName = "Cửa Hàng Bán Lẻ & Cà Phê TaxMate",
                    Address = "123 Đường Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh",
                    PreferElectronicInvoice = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-6),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-6)
                };
                db.BusinessProfiles.Add(business);
                await db.SaveChangesAsync();
                Console.WriteLine($"      Created BusinessProfile: {business.BusinessName} [Id: {business.Id}]");
            }

            // Categories & Suppliers
            var catDrink = await db.ProductCategories.FirstOrDefaultAsync(c => c.BusinessId == business.Id && c.Name == "Đồ Uống");
            if (catDrink == null)
            {
                catDrink = new ProductCategory { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Đồ Uống", Description = "Cà phê, trà, giải khát", SortOrder = 1 };
                db.ProductCategories.Add(catDrink);
            }

            var catFood = await db.ProductCategories.FirstOrDefaultAsync(c => c.BusinessId == business.Id && c.Name == "Đồ Ăn");
            if (catFood == null)
            {
                catFood = new ProductCategory { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Đồ Ăn", Description = "Bánh ngọt, đồ ăn nhẹ", SortOrder = 2 };
                db.ProductCategories.Add(catFood);
            }

            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.BusinessId == business.Id && s.Name == "Công Ty Cung Cấp Nông Sản Việt");
            if (supplier == null)
            {
                supplier = new Supplier { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Công Ty Cung Cấp Nông Sản Việt", ContactName = "Anh Nam", PhoneNumber = "0988777666", Address = "Lâm Đồng" };
                db.Suppliers.Add(supplier);
            }
            await db.SaveChangesAsync();

            // 2. Level 1 - Payment Accounts & EInvoice Config
            Console.WriteLine("[3/5] Seeding Payment Accounts & E-Invoice Config...");
            var payAccountCash = await db.PaymentAccounts.FirstOrDefaultAsync(p => p.BusinessId == business.Id && p.AccountNumber == "CASH");
            if (payAccountCash == null)
            {
                payAccountCash = new PaymentAccount
                {
                    PaymentAccountId = Guid.NewGuid(),
                    BusinessId = business.Id,
                    BankShortName = "CASH",
                    BankName = "Tiền mặt",
                    AccountNumber = "CASH",
                    AccountName = "Thu Ngân POS",
                    IsDefault = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-6),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-6)
                };
                db.PaymentAccounts.Add(payAccountCash);
            }

            var payAccountBank = await db.PaymentAccounts.FirstOrDefaultAsync(p => p.BusinessId == business.Id && p.AccountNumber == "999988886666");
            if (payAccountBank == null)
            {
                payAccountBank = new PaymentAccount
                {
                    PaymentAccountId = Guid.NewGuid(),
                    BusinessId = business.Id,
                    BankShortName = "MBBank",
                    BankName = "Ngân hàng MB",
                    AccountNumber = "999988886666",
                    AccountName = "NGUYEN TRUONG GIANG",
                    SePayBankAccountXid = "SEPAY-ACC-9999",
                    IsDefault = false,
                    CreatedAt = DateTime.UtcNow.AddMonths(-6),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-6)
                };
                db.PaymentAccounts.Add(payAccountBank);
            }

            var einvoiceConfig = await db.EInvoiceConfigs.FirstOrDefaultAsync(e => e.BusinessId == business.Id);
            if (einvoiceConfig == null)
            {
                einvoiceConfig = new EInvoiceConfig
                {
                    BusinessId = business.Id,
                    Provider = "SePay",
                    BaseUrl = "https://bankhub-api-sandbox.sepay.vn",
                    ClientId = "BH-SB-DEMO",
                    ClientSecret = "SECRET-DEMO",
                    InvoiceTemplateCode = "1/001",
                    Symbol = "C26TM",
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-6),
                    UpdatedAt = DateTime.UtcNow.AddMonths(-6)
                };
                db.EInvoiceConfigs.Add(einvoiceConfig);
            }
            await db.SaveChangesAsync();

            // 3. Level 1 - Ingredients & Stock Purchases (Calculating Moving Weighted Average Cost)
            Console.WriteLine("[4/5] Seeding Ingredients, Stock Purchases & Products (BOM & Price)...");
            
            // Ingredients
            var ingCoffee = await db.Ingredients.FirstOrDefaultAsync(i => i.BusinessId == business.Id && i.Name == "Hạt Cà Phê Arabica");
            if (ingCoffee == null)
            {
                ingCoffee = new Ingredient { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Hạt Cà Phê Arabica", Unit = "Kg", EstimatedPrice = 200000m, StockQuantity = 0, CreatedAt = DateTime.UtcNow.AddMonths(-6) };
                db.Ingredients.Add(ingCoffee);
            }

            var ingMilk = await db.Ingredients.FirstOrDefaultAsync(i => i.BusinessId == business.Id && i.Name == "Sữa Tươi Tiệt Trùng");
            if (ingMilk == null)
            {
                ingMilk = new Ingredient { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Sữa Tươi Tiệt Trùng", Unit = "Lít", EstimatedPrice = 30000m, StockQuantity = 0, CreatedAt = DateTime.UtcNow.AddMonths(-6) };
                db.Ingredients.Add(ingMilk);
            }
            await db.SaveChangesAsync();

            // Ingredient Purchase Batch (Initial Stock)
            var existingPurchases = await db.IngredientPurchases.Where(p => p.BusinessId == business.Id).ToListAsync();
            if (!existingPurchases.Any())
            {
                var pur1 = new IngredientPurchase
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    IngredientId = ingCoffee.Id,
                    SupplierId = supplier.Id,
                    SupplierName = supplier.Name,
                    Quantity = 100, // 100 Kg
                    TotalCost = 20000000m, // 20tr -> 200k/kg
                    PurchaseDate = DateTime.UtcNow.AddMonths(-5),
                    InvoiceNumber = "NK-CF-001",
                    CreatedAt = DateTime.UtcNow.AddMonths(-5)
                };
                ingCoffee.StockQuantity += pur1.Quantity;
                ingCoffee.EstimatedPrice = 200000m;

                var pur2 = new IngredientPurchase
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    IngredientId = ingMilk.Id,
                    SupplierId = supplier.Id,
                    SupplierName = supplier.Name,
                    Quantity = 200, // 200 Lít
                    TotalCost = 6000000m, // 6tr -> 30k/lít
                    PurchaseDate = DateTime.UtcNow.AddMonths(-5),
                    InvoiceNumber = "NK-MILK-001",
                    CreatedAt = DateTime.UtcNow.AddMonths(-5)
                };
                ingMilk.StockQuantity += pur2.Quantity;
                ingMilk.EstimatedPrice = 30000m;

                db.IngredientPurchases.AddRange(pur1, pur2);
                await db.SaveChangesAsync();
            }

            // Products (Simple Product & BOM Product)
            var prodCake = await db.Products.FirstOrDefaultAsync(p => p.BusinessId == business.Id && p.ProductCode == "SP-CAKE01");
            if (prodCake == null)
            {
                prodCake = new Product
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    ProductCode = "SP-CAKE01",
                    Name = "Bánh Mì Ngọt Phô Mai",
                    ProductCategoryId = catFood.Id,
                    Unit = "Cái",
                    CostPrice = 15000m,
                    StockQuantity = 500, // Direct stock
                    Status = ProductStatus.Active,
                    CreatedAt = DateTime.UtcNow.AddMonths(-5)
                };
                db.Products.Add(prodCake);
                await db.SaveChangesAsync();

                db.ProductPrices.Add(new ProductPrice
                {
                    Id = Guid.NewGuid(),
                    ProductId = prodCake.Id,
                    Price = 35000m,
                    ApplyDate = DateTime.UtcNow.AddMonths(-5),
                    CreatedAt = DateTime.UtcNow.AddMonths(-5)
                });
            }

            var prodLatte = await db.Products.FirstOrDefaultAsync(p => p.BusinessId == business.Id && p.ProductCode == "SP-LATTE01");
            if (prodLatte == null)
            {
                prodLatte = new Product
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    ProductCode = "SP-LATTE01",
                    Name = "Cà Phê Cốt Dừa / Latte",
                    ProductCategoryId = catDrink.Id,
                    Unit = "Ly",
                    CostPrice = 0m, // Calculated from BOM
                    StockQuantity = null, // Managed via BOM
                    Status = ProductStatus.Active,
                    CreatedAt = DateTime.UtcNow.AddMonths(-5)
                };
                db.Products.Add(prodLatte);
                await db.SaveChangesAsync();

                db.ProductPrices.Add(new ProductPrice
                {
                    Id = Guid.NewGuid(),
                    ProductId = prodLatte.Id,
                    Price = 45000m,
                    ApplyDate = DateTime.UtcNow.AddMonths(-5),
                    CreatedAt = DateTime.UtcNow.AddMonths(-5)
                });

                // BOM Recipe: 0.02 Kg Coffee (4,000 VND) + 0.1 Lít Milk (3,000 VND) = 7,000 VND Cost
                db.ProductIngredients.AddRange(
                    new ProductIngredient { ProductId = prodLatte.Id, IngredientId = ingCoffee.Id, Quantity = 0.02m },
                    new ProductIngredient { ProductId = prodLatte.Id, IngredientId = ingMilk.Id, Quantity = 0.1m }
                );
            }
            await db.SaveChangesAsync();

            // 4. Level 0 - Seed 5 Completed Sample POS Transactions
            Console.WriteLine("[5/5] Seeding 5 Completed Sample POS Transactions...");

            var sampleOrders = new List<(string Code, DateTime Date, string PayMethod, Guid? PayAccId, bool IsVAT, string? TaxCode, string? Company, List<(Product Prod, decimal Qty, decimal Price, decimal Cost)> Items)>();

            var now = DateTime.UtcNow;
            
            // Order 1: Cash payment, Simple Product (Bánh Mì)
            sampleOrders.Add((
                Code: "HD" + now.AddDays(-10).ToString("yyyyMMdd") + "-0001",
                Date: now.AddDays(-10),
                PayMethod: "Cash",
                PayAccId: payAccountCash.PaymentAccountId,
                IsVAT: false, TaxCode: null, Company: null,
                Items: new List<(Product, decimal, decimal, decimal)> { (prodCake, 2m, 35000m, 15000m) }
            ));

            // Order 2: Transfer payment, BOM Product (Latte)
            sampleOrders.Add((
                Code: "HD" + now.AddDays(-8).ToString("yyyyMMdd") + "-0002",
                Date: now.AddDays(-8),
                PayMethod: "Transfer",
                PayAccId: payAccountBank.PaymentAccountId,
                IsVAT: false, TaxCode: null, Company: null,
                Items: new List<(Product, decimal, decimal, decimal)> { (prodLatte, 3m, 45000m, 7000m) }
            ));

            // Order 3: Transfer + E-Invoice VAT
            sampleOrders.Add((
                Code: "HD" + now.AddDays(-5).ToString("yyyyMMdd") + "-0003",
                Date: now.AddDays(-5),
                PayMethod: "Transfer",
                PayAccId: payAccountBank.PaymentAccountId,
                IsVAT: true, TaxCode: "0101234567", Company: "Công Ty TNHH Giải Pháp Công Nghệ ABC",
                Items: new List<(Product, decimal, decimal, decimal)> { (prodCake, 5m, 35000m, 15000m), (prodLatte, 5m, 45000m, 7000m) }
            ));

            // Order 4: Cash payment + Large quantity + Discount
            sampleOrders.Add((
                Code: "HD" + now.AddDays(-3).ToString("yyyyMMdd") + "-0004",
                Date: now.AddDays(-3),
                PayMethod: "Cash",
                PayAccId: payAccountCash.PaymentAccountId,
                IsVAT: false, TaxCode: null, Company: null,
                Items: new List<(Product, decimal, decimal, decimal)> { (prodCake, 10m, 35000m, 15000m) }
            ));

            // Order 5: Transfer payment (Bank)
            sampleOrders.Add((
                Code: "HD" + now.AddDays(-1).ToString("yyyyMMdd") + "-0005",
                Date: now.AddDays(-1),
                PayMethod: "Transfer",
                PayAccId: payAccountBank.PaymentAccountId,
                IsVAT: false, TaxCode: null, Company: null,
                Items: new List<(Product, decimal, decimal, decimal)> { (prodLatte, 2m, 45000m, 7000m) }
            ));

            int createdCount = 0;
            decimal totalRevenueSeeded = 0;

            foreach (var ord in sampleOrders)
            {
                var exists = await db.Transactions.AnyAsync(t => t.BusinessId == business.Id && t.TransactionCode == ord.Code);
                if (exists) continue;

                var txId = Guid.NewGuid();
                var subTotal = ord.Items.Sum(i => i.Qty * i.Price);
                var total = subTotal;

                var tx = new Transaction
                {
                    TransactionId = txId,
                    BusinessId = business.Id,
                    TransactionCode = ord.Code,
                    TransactionDate = ord.Date,
                    SubTotal = subTotal,
                    DiscountAmount = 0,
                    SurchargeAmount = 0,
                    TotalAmount = total,
                    Status = "Completed",
                    TransactionType = TransactionTypes.Sale,
                    InvoiceId = ord.Code,
                    CreatedAt = ord.Date,
                    UpdatedAt = ord.Date
                };

                foreach (var item in ord.Items)
                {
                    var lineTotal = item.Qty * item.Price;
                    var costAmount = item.Qty * item.Cost;

                    var txItem = new TransactionItem
                    {
                        TransactionItemId = Guid.NewGuid(),
                        TransactionId = txId,
                        ProductId = item.Prod.Id,
                        ProductName = item.Prod.Name,
                        Unit = item.Prod.Unit,
                        UnitPrice = item.Price,
                        Quantity = item.Qty,
                        LineTotal = lineTotal,
                        UnitCost = item.Cost,
                        CostAmount = costAmount,
                        CreatedAt = ord.Date,
                        UpdatedAt = ord.Date
                    };
                    tx.TransactionItems.Add(txItem);

                    // Inventory deductions
                    if (item.Prod.ProductCode == "SP-CAKE01")
                    {
                        item.Prod.StockQuantity = (item.Prod.StockQuantity ?? 0) - item.Qty;
                    }
                    else if (item.Prod.ProductCode == "SP-LATTE01")
                    {
                        ingCoffee.StockQuantity -= 0.02m * item.Qty;
                        ingMilk.StockQuantity -= 0.1m * item.Qty;
                    }
                }

                // Payment Record
                var payment = new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    TransactionId = txId,
                    PaymentMethod = ord.PayMethod,
                    Amount = total,
                    PaymentAccountId = ord.PayAccId,
                    PaidAt = ord.Date,
                    CreatedAt = ord.Date,
                    UpdatedAt = ord.Date
                };
                tx.Payments.Add(payment);

                // Invoice Record
                var invoice = new Invoice
                {
                    InvoiceNumber = ord.Code,
                    BusinessId = business.Id,
                    TotalAmount = total,
                    IssueDate = ord.Date,
                    Status = "Issued",
                    BuyerTaxCode = ord.TaxCode,
                    BuyerCompanyName = ord.Company,
                    BuyerAddress = ord.IsVAT ? "Quận 3, TP.HCM" : null,
                    BuyerEmail = ord.IsVAT ? "ketoan@abc.com.vn" : null,
                    TaxAuthorityCode = ord.IsVAT ? "CQT-" + Guid.NewGuid().ToString("N")[..10].ToUpper() : null,
                    OfficialPdfUrl = ord.IsVAT ? $"https://sinvoice.sepay.vn/pdf/{ord.Code}.pdf" : null,
                    OfficialXmlUrl = ord.IsVAT ? $"https://sinvoice.sepay.vn/xml/{ord.Code}.xml" : null,
                    CreatedAt = ord.Date,
                    UpdatedAt = ord.Date
                };

                foreach (var item in ord.Items)
                {
                    invoice.InvoiceDetails.Add(new InvoiceDetail
                    {
                        ProductId = item.Prod.Id,
                        InvoiceId = ord.Code,
                        ProductName = item.Prod.Name,
                        UnitPrice = item.Price,
                        Quantity = item.Qty,
                        LineTotal = item.Qty * item.Price
                    });
                }

                db.Transactions.Add(tx);
                db.Invoices.Add(invoice);

                createdCount++;
                totalRevenueSeeded += total;
            }

            await db.SaveChangesAsync();

            Console.WriteLine($"\n[SUCCESS] Seeded {createdCount} Sample Completed POS Transactions!");
            Console.WriteLine($"          Total Revenue Seeded (Sample): {totalRevenueSeeded:N0} VNĐ");

            // Optional Scale Up to ~1 Billion VND
            if (scaleToBillion)
            {
                Console.WriteLine("\n[SCALE-UP] Flag --scale-billion detected. Generating additional past orders to reach 1,000,000,000 VNĐ...");
                decimal target = 1000000000m;
                decimal currentTotal = await db.Transactions.Where(t => t.BusinessId == business.Id && t.Status == "Completed").SumAsync(t => t.TotalAmount);

                int additionalCount = 0;
                var rand = new Random(42);

                while (currentTotal < target)
                {
                    var orderDate = now.AddDays(-rand.Next(1, 180)); // past 6 months
                    var orderNum = ++additionalCount + 100;
                    var code = $"HD{orderDate:yyyyMMdd}-{orderNum:D4}";

                    // Alternate products
                    var isLatte = rand.Next(0, 2) == 0;
                    var prod = isLatte ? prodLatte : prodCake;
                    decimal qty = rand.Next(1, 10);
                    decimal price = isLatte ? 45000m : 35000m;
                    decimal cost = isLatte ? 7000m : 15000m;
                    decimal lineTotal = qty * price;

                    var isVat = rand.Next(0, 5) == 0; // 20% VAT rate
                    var isCash = rand.Next(0, 2) == 0;

                    var txId = Guid.NewGuid();
                    var tx = new Transaction
                    {
                        TransactionId = txId,
                        BusinessId = business.Id,
                        TransactionCode = code,
                        TransactionDate = orderDate,
                        SubTotal = lineTotal,
                        DiscountAmount = 0,
                        SurchargeAmount = 0,
                        TotalAmount = lineTotal,
                        Status = "Completed",
                        TransactionType = TransactionTypes.Sale,
                        InvoiceId = code,
                        CreatedAt = orderDate,
                        UpdatedAt = orderDate
                    };

                    tx.TransactionItems.Add(new TransactionItem
                    {
                        TransactionItemId = Guid.NewGuid(),
                        TransactionId = txId,
                        ProductId = prod.Id,
                        ProductName = prod.Name,
                        Unit = prod.Unit,
                        UnitPrice = price,
                        Quantity = qty,
                        LineTotal = lineTotal,
                        UnitCost = cost,
                        CostAmount = qty * cost,
                        CreatedAt = orderDate,
                        UpdatedAt = orderDate
                    });

                    tx.Payments.Add(new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        TransactionId = txId,
                        PaymentMethod = isCash ? "Cash" : "Transfer",
                        Amount = lineTotal,
                        PaymentAccountId = isCash ? payAccountCash.PaymentAccountId : payAccountBank.PaymentAccountId,
                        PaidAt = orderDate,
                        CreatedAt = orderDate,
                        UpdatedAt = orderDate
                    });

                    var invoice = new Invoice
                    {
                        InvoiceNumber = code,
                        BusinessId = business.Id,
                        TotalAmount = lineTotal,
                        IssueDate = orderDate,
                        Status = "Issued",
                        BuyerTaxCode = isVat ? "03" + rand.Next(10000000, 99999999) : null,
                        BuyerCompanyName = isVat ? $"Công Ty Khách Hàng Doanh Nghiệp {orderNum}" : null,
                        TaxAuthorityCode = isVat ? "CQT-" + Guid.NewGuid().ToString("N")[..10].ToUpper() : null,
                        CreatedAt = orderDate,
                        UpdatedAt = orderDate
                    };

                    invoice.InvoiceDetails.Add(new InvoiceDetail
                    {
                        ProductId = prod.Id,
                        InvoiceId = code,
                        ProductName = prod.Name,
                        UnitPrice = price,
                        Quantity = qty,
                        LineTotal = lineTotal
                    });

                    db.Transactions.Add(tx);
                    db.Invoices.Add(invoice);

                    // Inventory deduction
                    if (isLatte)
                    {
                        ingCoffee.StockQuantity -= 0.02m * qty;
                        ingMilk.StockQuantity -= 0.1m * qty;
                    }
                    else
                    {
                        prodCake.StockQuantity = (prodCake.StockQuantity ?? 0) - qty;
                    }

                    currentTotal += lineTotal;

                    if (additionalCount % 100 == 0)
                    {
                        await db.SaveChangesAsync();
                        Console.WriteLine($"          Scaled {additionalCount} orders... Current Total: {currentTotal:N0} VNĐ");
                    }
                }

                await db.SaveChangesAsync();
                Console.WriteLine($"\n[SUCCESS] Scale-up completed! Added {additionalCount} transactions.");
                Console.WriteLine($"          Final Total Sales Revenue: {currentTotal:N0} VNĐ");
            }

            Console.WriteLine("\n=================================================");
            Console.WriteLine("     DATABASE RESET & SEEDING COMPLETED SUCCESS   ");
            Console.WriteLine("=================================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[ERROR] Seeding failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
