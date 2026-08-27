CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "BusinessCategories" (
    "BusinessCategoryId" uuid NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(1000),
    "VatRate" numeric(5,2) NOT NULL,
    "PitRate" numeric(5,2) NOT NULL,
    "IsActive" boolean NOT NULL,
    "EffectiveFrom" timestamp without time zone,
    "EffectiveTo" timestamp without time zone,
    "FormIndicatorCode" character varying(50),
    "FormSectionCode" character varying(20),
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_BusinessCategories" PRIMARY KEY ("BusinessCategoryId")
);

CREATE TABLE "LegalDocuments" (
    "LegalDocumentId" uuid NOT NULL,
    "DocumentCode" text NOT NULL,
    "DocumentName" text NOT NULL,
    "DocumentType" text,
    "AuthorityLevel" text,
    "EffectiveDate" timestamp without time zone,
    "ExpiredDate" timestamp without time zone,
    "Status" text NOT NULL,
    "SourceFileName" text NOT NULL,
    "StoragePath" text NOT NULL,
    "FileSize" bigint NOT NULL,
    "FileHash" text NOT NULL,
    "IsIndexed" boolean NOT NULL,
    "TotalPages" integer,
    "TotalChunks" integer,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_LegalDocuments" PRIMARY KEY ("LegalDocumentId")
);

CREATE TABLE "SubscriptionPlans" (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(1000),
    "MonthlyPrice" numeric(18,2) NOT NULL,
    "AnnualPrice" numeric(18,2) NOT NULL,
    "MaxProducts" integer,
    "MaxTransactionsPerMonth" integer,
    "IsActive" boolean NOT NULL,
    "SortOrder" integer NOT NULL,
    CONSTRAINT "PK_SubscriptionPlans" PRIMARY KEY ("Id")
);

CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Email" character varying(255) NOT NULL,
    "TaxCode" character varying(20),
    "PasswordHash" character varying(500),
    "GoogleId" character varying(100),
    "FullName" character varying(200) NOT NULL,
    "Phone" character varying(20),
    "Role" character varying(50) NOT NULL,
    "AvatarUrl" character varying(1000),
    "AccountStatus" character varying(20) NOT NULL,
    "EmailVerificationToken" character varying(128),
    "EmailVerificationTokenExpiresAt" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);

CREATE TABLE "PlanFeatures" (
    "Id" uuid NOT NULL,
    "SubscriptionPlanId" uuid NOT NULL,
    "FeatureKey" character varying(100) NOT NULL,
    "FeatureName" character varying(200) NOT NULL,
    "IsEnabled" boolean NOT NULL,
    CONSTRAINT "PK_PlanFeatures" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PlanFeatures_SubscriptionPlans_SubscriptionPlanId" FOREIGN KEY ("SubscriptionPlanId") REFERENCES "SubscriptionPlans" ("Id") ON DELETE CASCADE
);

CREATE TABLE "BusinessProfiles" (
    "Id" uuid NOT NULL,
    "OwnerId" uuid NOT NULL,
    "BusinessName" character varying(300) NOT NULL,
    "ProvinceCode" character varying(20),
    "WardCode" character varying(20),
    "Address" character varying(500),
    "MainCategoryId" uuid,
    "PreferElectronicInvoice" boolean NOT NULL,
    "IsStockTrackingEnabled" boolean NOT NULL,
    "SePayCompanyXid" character varying(100),
    "LastSePayLinkTokenXid" character varying(100),
    "IsActive" boolean NOT NULL,
    "TaxAdministrationAreaCode" character varying(50),
    "ManagingTaxAuthority" character varying(255),
    "TaxAuthorityLevel" character varying(20),
    "CollectingAuthority" character varying(255),
    "BusinessLocationCode" character varying(50),
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_BusinessProfiles" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_BusinessProfiles_BusinessCategories_MainCategoryId" FOREIGN KEY ("MainCategoryId") REFERENCES "BusinessCategories" ("BusinessCategoryId") ON DELETE SET NULL,
    CONSTRAINT "FK_BusinessProfiles_Users_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Notifications" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Message" character varying(2000) NOT NULL,
    "Type" character varying(50) NOT NULL,
    "IsRead" boolean NOT NULL,
    "ReadAt" timestamp without time zone,
    "ReferenceId" character varying(100),
    "ReferenceType" character varying(100),
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Notifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "UserDevices" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "DeviceToken" text NOT NULL,
    "Platform" text NOT NULL,
    "LastActiveAt" timestamp without time zone NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_UserDevices" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_UserDevices_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "UserSubscriptions" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "SubscriptionPlanId" uuid NOT NULL,
    "StartDate" timestamp without time zone NOT NULL,
    "EndDate" timestamp without time zone,
    "Status" character varying(50) NOT NULL,
    "BillingCycle" character varying(20) NOT NULL,
    "AutoRenew" boolean NOT NULL,
    "PaymentOrderCode" bigint,
    "PaymentLinkId" character varying(200),
    "CheckoutUrl" character varying(1000),
    "PaymentStatus" character varying(50) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_UserSubscriptions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_UserSubscriptions_SubscriptionPlans_SubscriptionPlanId" FOREIGN KEY ("SubscriptionPlanId") REFERENCES "SubscriptionPlans" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserSubscriptions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ChatConversations" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "BusinessId" uuid,
    "Title" text NOT NULL,
    "Status" text NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_ChatConversations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ChatConversations_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_ChatConversations_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "EInvoiceConfigs" (
    "BusinessId" uuid NOT NULL,
    "Provider" character varying(50) NOT NULL,
    "BaseUrl" character varying(500) NOT NULL,
    "ClientId" character varying(200) NOT NULL,
    "ClientSecret" character varying(500) NOT NULL,
    "ProviderAccountId" character varying(100),
    "InvoiceTemplateCode" character varying(50),
    "Symbol" character varying(50),
    "IsEnabled" boolean NOT NULL,
    "QuotaWarningThreshold" integer NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_EInvoiceConfigs" PRIMARY KEY ("BusinessId"),
    CONSTRAINT "FK_EInvoiceConfigs_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ExpenseCategories" (
    "ExpenseCategoryId" uuid NOT NULL,
    "CategoryName" character varying(100) NOT NULL,
    "Description" character varying(500),
    "IsDefault" boolean NOT NULL,
    "BusinessId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_ExpenseCategories" PRIMARY KEY ("ExpenseCategoryId"),
    CONSTRAINT "FK_ExpenseCategories_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "IncomeCategories" (
    "IncomeCategoryId" uuid NOT NULL,
    "CategoryName" character varying(100) NOT NULL,
    "Description" character varying(500),
    "IsDefault" boolean NOT NULL,
    "BusinessId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_IncomeCategories" PRIMARY KEY ("IncomeCategoryId"),
    CONSTRAINT "FK_IncomeCategories_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Ingredients" (
    "Id" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Unit" character varying(50),
    "EstimatedPrice" numeric(18,6),
    "StockQuantity" numeric(18,4) NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Ingredients" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Ingredients_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Invoices" (
    "InvoiceNumber" character varying(50) NOT NULL,
    "InvoiceTemplateCode" character varying(50),
    "Symbol" character varying(50),
    "BusinessId" uuid NOT NULL,
    "TotalAmount" numeric(18,2) NOT NULL,
    "IssueDate" timestamp without time zone NOT NULL,
    "Status" character varying(50) NOT NULL,
    "PdfUrl" character varying(1000),
    "TaxAuthorityCode" character varying(100),
    "OfficialPdfUrl" character varying(1000),
    "OfficialXmlUrl" character varying(1000),
    "SePayTrackingCode" character varying(100),
    "SePayReferenceCode" character varying(100),
    "SePayMessage" character varying(500),
    "BuyerTaxCode" character varying(20),
    "BuyerCompanyName" character varying(250),
    "BuyerAddress" character varying(500),
    "BuyerEmail" character varying(150),
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Invoices" PRIMARY KEY ("InvoiceNumber"),
    CONSTRAINT "FK_Invoices_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "PaymentAccounts" (
    "PaymentAccountId" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "BankShortName" character varying(50) NOT NULL,
    "BankName" character varying(200) NOT NULL,
    "AccountNumber" character varying(50) NOT NULL,
    "AccountName" character varying(200) NOT NULL,
    "IsDefault" boolean NOT NULL,
    "Description" character varying(500),
    "CassoAccessToken" character varying(1000),
    "CassoRefreshToken" character varying(500),
    "CassoConnectedAccountId" character varying(100),
    "SePayBankAccountXid" character varying(100),
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_PaymentAccounts" PRIMARY KEY ("PaymentAccountId"),
    CONSTRAINT "FK_PaymentAccounts_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ProductCategories" (
    "Id" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500),
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_ProductCategories" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProductCategories_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Suppliers" (
    "Id" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ContactName" character varying(200),
    "PhoneNumber" character varying(50),
    "Address" character varying(500),
    "Note" character varying(1000),
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Suppliers" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Suppliers_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "TaxPeriods" (
    "Id" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "PeriodType" character varying(20) NOT NULL,
    "Year" integer NOT NULL,
    "Month" integer,
    "Quarter" integer,
    "PeriodStartDate" timestamp without time zone NOT NULL,
    "PeriodEndDate" timestamp without time zone NOT NULL,
    "DueDate" timestamp without time zone,
    "Status" character varying(30) NOT NULL,
    "SalesRevenue" numeric(18,2) NOT NULL,
    "OtherRevenue" numeric(18,2) NOT NULL,
    "TotalRevenue" numeric(18,2) NOT NULL,
    "TaxableRevenue" numeric(18,2) NOT NULL,
    "VatTaxAmount" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxAmount" numeric(18,2) NOT NULL,
    "EstimatedTax" numeric(18,2) NOT NULL,
    "TaxAmountDebt" numeric(18,2) NOT NULL,
    "ClosedAt" timestamp without time zone,
    "CalculatedAt" timestamp without time zone,
    "SubmittedAt" timestamp without time zone,
    "PaidDate" timestamp without time zone,
    "BusinessProfileId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_TaxPeriods" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TaxPeriods_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_TaxPeriods_BusinessProfiles_BusinessProfileId" FOREIGN KEY ("BusinessProfileId") REFERENCES "BusinessProfiles" ("Id")
);

CREATE TABLE "ChatMessages" (
    "Id" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "Role" text NOT NULL,
    "Content" text NOT NULL,
    "PromptTokens" integer NOT NULL,
    "CompletionTokens" integer NOT NULL,
    "TotalTokens" integer NOT NULL,
    "ModelName" text,
    "CreatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_ChatMessages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ChatMessages_ChatConversations_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "ChatConversations" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Incomes" (
    "IncomeId" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "IncomeCategoryId" uuid NOT NULL,
    "IncomeTitle" character varying(200) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "IncomeDate" timestamp without time zone NOT NULL,
    "PaymentMethod" character varying(50),
    "ReceiptImageUrl" character varying(1000),
    "Note" character varying(2000),
    "FileUrl" character varying(1000),
    "DueDate" timestamp without time zone,
    "ReceivedDate" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Incomes" PRIMARY KEY ("IncomeId"),
    CONSTRAINT "FK_Incomes_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Incomes_IncomeCategories_IncomeCategoryId" FOREIGN KEY ("IncomeCategoryId") REFERENCES "IncomeCategories" ("IncomeCategoryId") ON DELETE RESTRICT
);

CREATE TABLE "Transactions" (
    "TransactionId" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "TransactionCode" character varying(100) NOT NULL,
    "TransactionDate" timestamp without time zone NOT NULL,
    "SubTotal" numeric(18,2) NOT NULL,
    "DiscountType" character varying(20),
    "DiscountValue" numeric(18,2),
    "DiscountAmount" numeric(18,2) NOT NULL,
    "SurchargeName" character varying(100),
    "SurchargeType" character varying(20),
    "SurchargeValue" numeric(18,2),
    "SurchargeAmount" numeric(18,2) NOT NULL,
    "TotalAmount" numeric(18,2) NOT NULL,
    "InvoiceId" character varying(50),
    "Status" character varying(50) NOT NULL,
    "Note" character varying(2000),
    "TransactionType" character varying(30) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Transactions" PRIMARY KEY ("TransactionId"),
    CONSTRAINT "FK_Transactions_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Transactions_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("InvoiceNumber") ON DELETE SET NULL
);

CREATE TABLE "Products" (
    "Id" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "ProductCode" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ProductCategoryId" uuid,
    "BusinessCategoryId" uuid,
    "Description" character varying(2000),
    "Unit" character varying(50),
    "ImageUrl" character varying(1000),
    "CostPrice" numeric(18,6),
    "StockQuantity" numeric(18,4),
    "Status" character varying(50) NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Products" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Products_BusinessCategories_BusinessCategoryId" FOREIGN KEY ("BusinessCategoryId") REFERENCES "BusinessCategories" ("BusinessCategoryId") ON DELETE SET NULL,
    CONSTRAINT "FK_Products_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Products_ProductCategories_ProductCategoryId" FOREIGN KEY ("ProductCategoryId") REFERENCES "ProductCategories" ("Id") ON DELETE SET NULL
);

CREATE TABLE "Expenses" (
    "ExpenseId" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "ExpenseCategoryId" uuid NOT NULL,
    "ExpenseTitle" character varying(200) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "ExpenseDate" timestamp without time zone NOT NULL,
    "PaymentMethod" character varying(50),
    "ReceiptImageUrl" character varying(1000),
    "Note" character varying(2000),
    "FileUrl" character varying(1000),
    "DueDate" timestamp without time zone,
    "PaidDate" timestamp without time zone,
    "SupplierId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Expenses" PRIMARY KEY ("ExpenseId"),
    CONSTRAINT "FK_Expenses_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Expenses_ExpenseCategories_ExpenseCategoryId" FOREIGN KEY ("ExpenseCategoryId") REFERENCES "ExpenseCategories" ("ExpenseCategoryId") ON DELETE RESTRICT,
    CONSTRAINT "FK_Expenses_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE SET NULL
);

CREATE TABLE "IngredientPurchases" (
    "Id" uuid NOT NULL,
    "IngredientId" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "Quantity" numeric(18,3) NOT NULL,
    "TotalCost" numeric(18,2) NOT NULL,
    "PurchaseDate" timestamp without time zone NOT NULL,
    "InvoiceNumber" character varying(100),
    "SupplierName" character varying(200),
    "ReceiptImageUrl" character varying(1000),
    "SupplierId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_IngredientPurchases" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IngredientPurchases_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_IngredientPurchases_Ingredients_IngredientId" FOREIGN KEY ("IngredientId") REFERENCES "Ingredients" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_IngredientPurchases_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE SET NULL
);

CREATE TABLE "TaxCalculations" (
    "Id" uuid NOT NULL,
    "TaxPeriodId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "Status" character varying(30) NOT NULL,
    "CalculationRuleVersion" character varying(100),
    "TotalRevenue" numeric(18,2) NOT NULL,
    "TotalTaxableRevenue" numeric(18,2) NOT NULL,
    "TotalVatTaxAmount" numeric(18,2) NOT NULL,
    "TotalPersonalIncomeTaxAmount" numeric(18,2) NOT NULL,
    "TotalTaxBeforeExemption" numeric(18,2) NOT NULL,
    "TotalExemptionAmount" numeric(18,2) NOT NULL,
    "TotalTaxPayableAmount" numeric(18,2) NOT NULL,
    "AnnualRevenueAtCalculation" numeric(18,2) NOT NULL,
    "ApplicableRevenueThreshold" numeric(18,2) NOT NULL,
    "RecommendedFormCode" character varying(30) NOT NULL,
    "RemainingPitDeduction" numeric(18,2) NOT NULL,
    "CalculatedAt" timestamp without time zone NOT NULL,
    "CalculatedByUserId" uuid,
    "IsCurrent" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_TaxCalculations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TaxCalculations_TaxPeriods_TaxPeriodId" FOREIGN KEY ("TaxPeriodId") REFERENCES "TaxPeriods" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ChatReferences" (
    "Id" uuid NOT NULL,
    "MessageId" uuid NOT NULL,
    "LegalDocumentId" uuid NOT NULL,
    "ChunkId" text NOT NULL,
    "SimilarityScore" double precision NOT NULL,
    CONSTRAINT "PK_ChatReferences" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ChatReferences_ChatMessages_MessageId" FOREIGN KEY ("MessageId") REFERENCES "ChatMessages" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ChatReferences_LegalDocuments_LegalDocumentId" FOREIGN KEY ("LegalDocumentId") REFERENCES "LegalDocuments" ("LegalDocumentId") ON DELETE RESTRICT
);

CREATE TABLE "Payments" (
    "PaymentId" uuid NOT NULL,
    "TransactionId" uuid NOT NULL,
    "PaymentMethod" character varying(50) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "PaymentAccountId" uuid,
    "PaidAt" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_Payments" PRIMARY KEY ("PaymentId"),
    CONSTRAINT "FK_Payments_PaymentAccounts_PaymentAccountId" FOREIGN KEY ("PaymentAccountId") REFERENCES "PaymentAccounts" ("PaymentAccountId") ON DELETE SET NULL,
    CONSTRAINT "FK_Payments_Transactions_TransactionId" FOREIGN KEY ("TransactionId") REFERENCES "Transactions" ("TransactionId") ON DELETE CASCADE
);

CREATE TABLE "InvoiceDetails" (
    "ProductId" uuid NOT NULL,
    "InvoiceId" character varying(50) NOT NULL,
    "ProductName" character varying(300) NOT NULL,
    "UnitPrice" numeric(18,2) NOT NULL,
    "Quantity" numeric(18,3) NOT NULL,
    "LineTotal" numeric(18,2) NOT NULL,
    CONSTRAINT "PK_InvoiceDetails" PRIMARY KEY ("ProductId", "InvoiceId"),
    CONSTRAINT "FK_InvoiceDetails_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("InvoiceNumber") ON DELETE CASCADE,
    CONSTRAINT "FK_InvoiceDetails_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ProductIngredients" (
    "ProductId" uuid NOT NULL,
    "IngredientId" uuid NOT NULL,
    "Quantity" numeric(18,3) NOT NULL,
    CONSTRAINT "PK_ProductIngredients" PRIMARY KEY ("ProductId", "IngredientId"),
    CONSTRAINT "FK_ProductIngredients_Ingredients_IngredientId" FOREIGN KEY ("IngredientId") REFERENCES "Ingredients" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProductIngredients_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ProductPrices" (
    "Id" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Price" numeric(18,2) NOT NULL,
    "ApplyDate" timestamp without time zone NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_ProductPrices" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProductPrices_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
);

CREATE TABLE "TransactionItems" (
    "TransactionItemId" uuid NOT NULL,
    "TransactionId" uuid NOT NULL,
    "ProductId" uuid,
    "ProductName" character varying(300) NOT NULL,
    "Unit" character varying(50),
    "UnitPrice" numeric(18,2) NOT NULL,
    "Quantity" numeric(18,3) NOT NULL,
    "DiscountType" character varying(20),
    "DiscountValue" numeric(18,2),
    "DiscountAmount" numeric(18,2) NOT NULL,
    "LineTotal" numeric(18,2) NOT NULL,
    "Note" character varying(500),
    "UnitCost" numeric(18,2) NOT NULL,
    "CostAmount" numeric(18,2) NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_TransactionItems" PRIMARY KEY ("TransactionItemId"),
    CONSTRAINT "FK_TransactionItems_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_TransactionItems_Transactions_TransactionId" FOREIGN KEY ("TransactionId") REFERENCES "Transactions" ("TransactionId") ON DELETE CASCADE
);

CREATE TABLE "TaxCalculationLines" (
    "Id" uuid NOT NULL,
    "TaxCalculationId" uuid NOT NULL,
    "SectionCode" character varying(20) NOT NULL,
    "IndicatorCode" character varying(20) NOT NULL,
    "BusinessActivityCode" character varying(50) NOT NULL,
    "BusinessActivityName" character varying(255) NOT NULL,
    "BusinessLocationId" uuid,
    "BusinessLocationCode" character varying(50),
    "TotalRevenue" numeric(18,2) NOT NULL,
    "VatTaxableRevenue" numeric(18,2) NOT NULL,
    "ZeroRatedVatRevenue" numeric(18,2) NOT NULL,
    "VatTaxRate" numeric(9,4) NOT NULL,
    "VatTaxAmount" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxableRevenue" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxDeductibleRevenue" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxRate" numeric(9,4) NOT NULL,
    "PersonalIncomeTaxAmount" numeric(18,2) NOT NULL,
    "VatNonTaxableRevenue" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxRevenue" numeric(18,2) NOT NULL,
    "DisplayOrder" integer NOT NULL,
    "BusinessCategoryId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_TaxCalculationLines" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TaxCalculationLines_BusinessCategories_BusinessCategoryId" FOREIGN KEY ("BusinessCategoryId") REFERENCES "BusinessCategories" ("BusinessCategoryId"),
    CONSTRAINT "FK_TaxCalculationLines_TaxCalculations_TaxCalculationId" FOREIGN KEY ("TaxCalculationId") REFERENCES "TaxCalculations" ("Id") ON DELETE CASCADE
);

CREATE TABLE "TaxDeclarations" (
    "Id" uuid NOT NULL,
    "TaxPeriodId" uuid NOT NULL,
    "TaxCalculationId" uuid NOT NULL,
    "FormCode" character varying(30) NOT NULL,
    "DeclarationCode" character varying(50) NOT NULL,
    "Version" integer NOT NULL,
    "DeclarationType" character varying(30) NOT NULL,
    "SupplementNumber" integer,
    "Status" character varying(30) NOT NULL,
    "TaxpayerName" character varying(255) NOT NULL,
    "TaxCode" character varying(50) NOT NULL,
    "TaxpayerAddress" character varying(500),
    "AuthorizedDeclarerName" character varying(255),
    "AuthorizedDeclarerTaxCode" character varying(50),
    "TaxAgentName" character varying(255),
    "TaxAgentTaxCode" character varying(50),
    "TaxAgentContractNumber" character varying(100),
    "TaxAgentContractDate" timestamp without time zone,
    "TotalRevenue" numeric(18,2) NOT NULL,
    "TotalVatTaxAmount" numeric(18,2) NOT NULL,
    "TotalPersonalIncomeTaxAmount" numeric(18,2) NOT NULL,
    "VatExemptionAmount" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxExemptionAmount" numeric(18,2) NOT NULL,
    "VatPayableAmount" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxPayableAmount" numeric(18,2) NOT NULL,
    "TotalTaxPayableAmount" numeric(18,2) NOT NULL,
    "GeneratedAt" timestamp without time zone NOT NULL,
    "SubmittedAt" timestamp without time zone,
    "SubmissionMethod" character varying(50),
    "SubmissionReference" character varying(255),
    "PdfFileUrl" character varying(1000),
    "XmlFileUrl" character varying(1000),
    "RemainingPitDeduction" numeric(18,2) NOT NULL,
    "IsCurrent" boolean NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_TaxDeclarations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TaxDeclarations_TaxCalculations_TaxCalculationId" FOREIGN KEY ("TaxCalculationId") REFERENCES "TaxCalculations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_TaxDeclarations_TaxPeriods_TaxPeriodId" FOREIGN KEY ("TaxPeriodId") REFERENCES "TaxPeriods" ("Id") ON DELETE CASCADE
);

CREATE TABLE "TaxDeclarationLines" (
    "Id" uuid NOT NULL,
    "TaxDeclarationId" uuid NOT NULL,
    "SectionCode" character varying(20) NOT NULL,
    "IndicatorCode" character varying(20) NOT NULL,
    "BusinessActivityCode" character varying(50) NOT NULL,
    "BusinessActivityName" character varying(255) NOT NULL,
    "BusinessLocationId" uuid,
    "BusinessLocationCode" character varying(50),
    "TotalRevenue" numeric(18,2) NOT NULL,
    "VatTaxableRevenue" numeric(18,2) NOT NULL,
    "ZeroRatedVatRevenue" numeric(18,2) NOT NULL,
    "VatTaxRate" numeric(9,4) NOT NULL,
    "VatTaxAmount" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxableRevenue" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxDeductibleRevenue" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxRate" numeric(9,4) NOT NULL,
    "PersonalIncomeTaxAmount" numeric(18,2) NOT NULL,
    "VatNonTaxableRevenue" numeric(18,2) NOT NULL,
    "PersonalIncomeTaxRevenue" numeric(18,2) NOT NULL,
    "DisplayOrder" integer NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_TaxDeclarationLines" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TaxDeclarationLines_TaxDeclarations_TaxDeclarationId" FOREIGN KEY ("TaxDeclarationId") REFERENCES "TaxDeclarations" ("Id") ON DELETE CASCADE
);

CREATE TABLE "TaxDeclarationObligations" (
    "Id" uuid NOT NULL,
    "TaxDeclarationId" uuid NOT NULL,
    "TaxType" character varying(50) NOT NULL,
    "BusinessLocationCode" character varying(50),
    "StateBudgetContent" character varying(500),
    "IndicatorCode" character varying(50),
    "AssessedAmount" numeric(18,2) NOT NULL,
    "ExemptionAmount" numeric(18,2) NOT NULL,
    "PayableAmount" numeric(18,2) NOT NULL,
    "StateBudgetChapterCode" character varying(50),
    "StateBudgetSubsectionCode" character varying(50),
    "AdministrativeAreaCode" character varying(50),
    "CollectingAuthority" character varying(255),
    "TaxAuthority" character varying(255),
    "DueDate" timestamp without time zone,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_TaxDeclarationObligations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TaxDeclarationObligations_TaxDeclarations_TaxDeclarationId" FOREIGN KEY ("TaxDeclarationId") REFERENCES "TaxDeclarations" ("Id") ON DELETE CASCADE
);

CREATE TABLE "TaxPayments" (
    "Id" uuid NOT NULL,
    "TaxPeriodId" uuid NOT NULL,
    "TaxDeclarationId" uuid,
    "PaymentCode" character varying(50) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "PaymentDate" timestamp without time zone NOT NULL,
    "PaymentMethod" character varying(30) NOT NULL,
    "Status" character varying(30) NOT NULL,
    "TransactionReference" character varying(255),
    "StateBudgetChapterCode" character varying(50),
    "StateBudgetSubsectionCode" character varying(50),
    "AdministrativeAreaCode" character varying(50),
    "ReceiptFileUrl" character varying(1000),
    "Note" character varying(1000),
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_TaxPayments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TaxPayments_TaxDeclarations_TaxDeclarationId" FOREIGN KEY ("TaxDeclarationId") REFERENCES "TaxDeclarations" ("Id"),
    CONSTRAINT "FK_TaxPayments_TaxPeriods_TaxPeriodId" FOREIGN KEY ("TaxPeriodId") REFERENCES "TaxPeriods" ("Id") ON DELETE RESTRICT
);

INSERT INTO "BusinessCategories" ("BusinessCategoryId", "Code", "CreatedAt", "Description", "EffectiveFrom", "EffectiveTo", "FormIndicatorCode", "FormSectionCode", "IsActive", "Name", "PitRate", "UpdatedAt", "VatRate")
VALUES ('a0000001-0000-4000-8000-000000000001', 'DIST_GOODS', TIMESTAMP '2026-01-01T00:00:00', 'GTGT 1%, TNCN 0.5%', NULL, NULL, NULL, NULL, TRUE, 'Phân phối, cung cấp hàng hóa', 0.5, TIMESTAMP '2026-01-01T00:00:00', 1.0);
INSERT INTO "BusinessCategories" ("BusinessCategoryId", "Code", "CreatedAt", "Description", "EffectiveFrom", "EffectiveTo", "FormIndicatorCode", "FormSectionCode", "IsActive", "Name", "PitRate", "UpdatedAt", "VatRate")
VALUES ('a0000001-0000-4000-8000-000000000002', 'PROD_TRANSPORT', TIMESTAMP '2026-01-01T00:00:00', 'GTGT 3%, TNCN 1.5%', NULL, NULL, NULL, NULL, TRUE, 'Sản xuất, vận tải, dịch vụ gắn HH, XD có NVL', 1.5, TIMESTAMP '2026-01-01T00:00:00', 3.0);
INSERT INTO "BusinessCategories" ("BusinessCategoryId", "Code", "CreatedAt", "Description", "EffectiveFrom", "EffectiveTo", "FormIndicatorCode", "FormSectionCode", "IsActive", "Name", "PitRate", "UpdatedAt", "VatRate")
VALUES ('a0000001-0000-4000-8000-000000000003', 'SERVICE_CONSTRUCT', TIMESTAMP '2026-01-01T00:00:00', 'GTGT 5%, TNCN 2%', NULL, NULL, NULL, NULL, TRUE, 'Dịch vụ, XD không bao thầu NVL', 2.0, TIMESTAMP '2026-01-01T00:00:00', 5.0);
INSERT INTO "BusinessCategories" ("BusinessCategoryId", "Code", "CreatedAt", "Description", "EffectiveFrom", "EffectiveTo", "FormIndicatorCode", "FormSectionCode", "IsActive", "Name", "PitRate", "UpdatedAt", "VatRate")
VALUES ('a0000001-0000-4000-8000-000000000004', 'ASSET_INSURANCE', TIMESTAMP '2026-01-01T00:00:00', 'GTGT 5%, TNCN 5%', NULL, NULL, NULL, NULL, TRUE, 'Cho thuê tài sản / đại lý BH, xổ số, BHĐC…', 5.0, TIMESTAMP '2026-01-01T00:00:00', 5.0);
INSERT INTO "BusinessCategories" ("BusinessCategoryId", "Code", "CreatedAt", "Description", "EffectiveFrom", "EffectiveTo", "FormIndicatorCode", "FormSectionCode", "IsActive", "Name", "PitRate", "UpdatedAt", "VatRate")
VALUES ('a0000001-0000-4000-8000-000000000005', 'OTHER', TIMESTAMP '2026-01-01T00:00:00', 'GTGT 2%, TNCN 1%', NULL, NULL, NULL, NULL, TRUE, 'Hoạt động khác', 1.0, TIMESTAMP '2026-01-01T00:00:00', 2.0);
INSERT INTO "BusinessCategories" ("BusinessCategoryId", "Code", "CreatedAt", "Description", "EffectiveFrom", "EffectiveTo", "FormIndicatorCode", "FormSectionCode", "IsActive", "Name", "PitRate", "UpdatedAt", "VatRate")
VALUES ('d1111111-1111-1111-1111-111111111111', 'FNB', TIMESTAMP '-infinity', 'Hoạt động dịch vụ ăn uống có gắn với hàng hóa.', TIMESTAMP '2026-01-01T00:00:00', NULL, 'd', 'I', TRUE, 'Ăn uống, nhà hàng, F&B', 1.5, TIMESTAMP '-infinity', 3.0);
INSERT INTO "BusinessCategories" ("BusinessCategoryId", "Code", "CreatedAt", "Description", "EffectiveFrom", "EffectiveTo", "FormIndicatorCode", "FormSectionCode", "IsActive", "Name", "PitRate", "UpdatedAt", "VatRate")
VALUES ('d2222222-2222-2222-2222-222222222222', 'SERVICE', TIMESTAMP '-infinity', 'Dịch vụ, xây dựng không bao thầu nguyên vật liệu.', TIMESTAMP '2026-01-01T00:00:00', NULL, 'b', 'I', TRUE, 'Dịch vụ', 2.0, TIMESTAMP '-infinity', 5.0);

INSERT INTO "SubscriptionPlans" ("Id", "AnnualPrice", "Description", "IsActive", "MaxProducts", "MaxTransactionsPerMonth", "MonthlyPrice", "Name", "SortOrder")
VALUES ('a1d1c694-d271-460b-8835-2b2e6a1b8c1d', 0.0, 'Trải nghiệm các tính năng quản lý cơ bản', TRUE, 50, 100, 0.0, 'Gói Miễn Phí', 0);
INSERT INTO "SubscriptionPlans" ("Id", "AnnualPrice", "Description", "IsActive", "MaxProducts", "MaxTransactionsPerMonth", "MonthlyPrice", "Name", "SortOrder")
VALUES ('b2d2c694-d271-460b-8835-2b2e6a1b8c2d', 990000.0, 'Phù hợp cho hộ kinh doanh cá thể nhỏ', TRUE, 500, 1000, 99000.0, 'Gói Hộ Kinh Doanh', 1);
INSERT INTO "SubscriptionPlans" ("Id", "AnnualPrice", "Description", "IsActive", "MaxProducts", "MaxTransactionsPerMonth", "MonthlyPrice", "Name", "SortOrder")
VALUES ('c3d3c694-d271-460b-8835-2b2e6a1b8c3d', 1990000.0, 'Giải pháp toàn diện cho doanh nghiệp tăng trưởng', TRUE, NULL, NULL, 199000.0, 'Gói Doanh Nghiệp Cao Cấp', 2);

INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('b1111111-1111-1111-1111-111111111111', 'revenue_recording', 'Ghi nhận doanh thu', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('b2222222-2222-2222-2222-222222222222', 'revenue_aggregation_viz', 'Tổng hợp doanh thu theo tháng/năm', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('b3333333-3333-3333-3333-333333333333', 'daily_revenue_reporting', 'Báo cáo doanh thu hàng ngày', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('b4444444-4444-4444-4444-444444444444', 'order_history_tracking', 'Theo dõi lịch sử đơn hàng', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('b5555555-5555-5555-5555-555555555555', 'best_selling_categories', 'Danh mục sản phẩm bán chạy nhất', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('b6666666-6666-6666-6666-666666666666', 'product_management', 'Quản lý sản phẩm', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('b7777777-7777-7777-7777-777777777777', 'expense_recording_monitoring', 'Ghi nhận & giám sát chi phí', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('b8888888-8888-8888-8888-888888888888', 'estimated_profitability_dashboard', 'Bảng điều khiển lợi nhuận ước tính', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('b9999999-9999-9999-9999-999999999999', 'ai_tax_guidance', 'Tư vấn thuế AI', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('baaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'rag_legal_retrieval', 'Tra cứu thông tin luật RAG', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'business_insight_reports', 'Báo cáo insight kinh doanh', TRUE, 'b2d2c694-d271-460b-8835-2b2e6a1b8c2d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('e1111111-1111-1111-1111-111111111111', 'revenue_recording', 'Ghi nhận doanh thu', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('e2222222-2222-2222-2222-222222222222', 'revenue_aggregation_viz', 'Tổng hợp doanh thu theo tháng/năm', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('e3333333-3333-3333-3333-333333333333', 'daily_revenue_reporting', 'Báo cáo doanh thu hàng ngày', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('e4444444-4444-4444-4444-444444444444', 'order_history_tracking', 'Theo dõi lịch sử đơn hàng', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('e5555555-5555-5555-5555-555555555555', 'best_selling_categories', 'Danh mục sản phẩm bán chạy nhất', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('e6666666-6666-6666-6666-666666666666', 'product_management', 'Quản lý sản phẩm', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('e7777777-7777-7777-7777-777777777777', 'expense_recording_monitoring', 'Ghi nhận & giám sát chi phí', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('e8888888-8888-8888-8888-888888888888', 'estimated_profitability_dashboard', 'Bảng điều khiển lợi nhuận ước tính', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('e9999999-9999-9999-9999-999999999999', 'ai_tax_guidance', 'Tư vấn thuế AI', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('eaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'rag_legal_retrieval', 'Tra cứu thông tin luật RAG', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('eaaaaaaa-cccc-cccc-cccc-cccccccccccc', 'einvoice_integration', 'Tích hợp hóa đơn điện tử', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('ebbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'business_insight_reports', 'Báo cáo insight kinh doanh', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('ebbbbbbb-dddd-dddd-dddd-dddddddddddd', 'advanced_analytics', 'Phân tích kinh doanh nâng cao', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('ececcccc-eeee-eeee-eeee-eeeeeeeeeeee', 'growth_readiness_monitoring', 'Giám sát mức độ sẵn sàng tăng trưởng', TRUE, 'c3d3c694-d271-460b-8835-2b2e6a1b8c3d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('f1111111-1111-1111-1111-111111111111', 'revenue_recording', 'Ghi nhận doanh thu', TRUE, 'a1d1c694-d271-460b-8835-2b2e6a1b8c1d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('f2222222-2222-2222-2222-222222222222', 'revenue_aggregation_viz', 'Tổng hợp doanh thu theo tháng/năm', TRUE, 'a1d1c694-d271-460b-8835-2b2e6a1b8c1d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('f3333333-3333-3333-3333-333333333333', 'daily_revenue_reporting', 'Báo cáo doanh thu hàng ngày', TRUE, 'a1d1c694-d271-460b-8835-2b2e6a1b8c1d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('f4444444-4444-4444-4444-444444444444', 'order_history_tracking', 'Theo dõi lịch sử đơn hàng', TRUE, 'a1d1c694-d271-460b-8835-2b2e6a1b8c1d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('f5555555-5555-5555-5555-555555555555', 'best_selling_categories', 'Danh mục sản phẩm bán chạy nhất', TRUE, 'a1d1c694-d271-460b-8835-2b2e6a1b8c1d');
INSERT INTO "PlanFeatures" ("Id", "FeatureKey", "FeatureName", "IsEnabled", "SubscriptionPlanId")
VALUES ('f6666666-6666-6666-6666-666666666666', 'product_management', 'Quản lý sản phẩm', TRUE, 'a1d1c694-d271-460b-8835-2b2e6a1b8c1d');

CREATE UNIQUE INDEX "IX_BusinessCategories_Code" ON "BusinessCategories" ("Code");

CREATE INDEX "IX_BusinessCategories_Name" ON "BusinessCategories" ("Name");

CREATE INDEX "IX_BusinessProfiles_MainCategoryId" ON "BusinessProfiles" ("MainCategoryId");

CREATE INDEX "IX_BusinessProfiles_OwnerId" ON "BusinessProfiles" ("OwnerId");

CREATE INDEX "IX_ChatConversations_BusinessId" ON "ChatConversations" ("BusinessId");

CREATE INDEX "IX_ChatConversations_UserId" ON "ChatConversations" ("UserId");

CREATE INDEX "IX_ChatConversations_UserId_Status" ON "ChatConversations" ("UserId", "Status");

CREATE INDEX "IX_ChatMessages_ConversationId" ON "ChatMessages" ("ConversationId");

CREATE INDEX "IX_ChatMessages_CreatedAt" ON "ChatMessages" ("CreatedAt");

CREATE INDEX "IX_ChatReferences_LegalDocumentId" ON "ChatReferences" ("LegalDocumentId");

CREATE INDEX "IX_ChatReferences_MessageId" ON "ChatReferences" ("MessageId");

CREATE INDEX "IX_ExpenseCategories_BusinessId" ON "ExpenseCategories" ("BusinessId");

CREATE UNIQUE INDEX "IX_ExpenseCategories_BusinessId_CategoryName" ON "ExpenseCategories" ("BusinessId", "CategoryName");

CREATE INDEX "IX_Expenses_BusinessId" ON "Expenses" ("BusinessId");

CREATE INDEX "IX_Expenses_BusinessId_ExpenseDate" ON "Expenses" ("BusinessId", "ExpenseDate");

CREATE INDEX "IX_Expenses_ExpenseCategoryId" ON "Expenses" ("ExpenseCategoryId");

CREATE INDEX "IX_Expenses_ExpenseDate" ON "Expenses" ("ExpenseDate");

CREATE INDEX "IX_Expenses_SupplierId" ON "Expenses" ("SupplierId");

CREATE INDEX "IX_IncomeCategories_BusinessId" ON "IncomeCategories" ("BusinessId");

CREATE UNIQUE INDEX "IX_IncomeCategories_BusinessId_CategoryName" ON "IncomeCategories" ("BusinessId", "CategoryName");

CREATE INDEX "IX_Incomes_BusinessId" ON "Incomes" ("BusinessId");

CREATE INDEX "IX_Incomes_BusinessId_IncomeDate" ON "Incomes" ("BusinessId", "IncomeDate");

CREATE INDEX "IX_Incomes_IncomeCategoryId" ON "Incomes" ("IncomeCategoryId");

CREATE INDEX "IX_Incomes_IncomeDate" ON "Incomes" ("IncomeDate");

CREATE INDEX "IX_IngredientPurchases_BusinessId" ON "IngredientPurchases" ("BusinessId");

CREATE INDEX "IX_IngredientPurchases_BusinessId_PurchaseDate" ON "IngredientPurchases" ("BusinessId", "PurchaseDate");

CREATE INDEX "IX_IngredientPurchases_IngredientId" ON "IngredientPurchases" ("IngredientId");

CREATE INDEX "IX_IngredientPurchases_InvoiceNumber" ON "IngredientPurchases" ("InvoiceNumber");

CREATE INDEX "IX_IngredientPurchases_PurchaseDate" ON "IngredientPurchases" ("PurchaseDate");

CREATE INDEX "IX_IngredientPurchases_SupplierId" ON "IngredientPurchases" ("SupplierId");

CREATE INDEX "IX_Ingredients_BusinessId" ON "Ingredients" ("BusinessId");

CREATE INDEX "IX_Ingredients_BusinessId_Name" ON "Ingredients" ("BusinessId", "Name");

CREATE INDEX "IX_InvoiceDetails_InvoiceId" ON "InvoiceDetails" ("InvoiceId");

CREATE INDEX "IX_Invoices_BusinessId" ON "Invoices" ("BusinessId");

CREATE INDEX "IX_Invoices_BusinessId_IssueDate" ON "Invoices" ("BusinessId", "IssueDate");

CREATE INDEX "IX_Invoices_IssueDate" ON "Invoices" ("IssueDate");

CREATE INDEX "IX_Invoices_Status" ON "Invoices" ("Status");

CREATE UNIQUE INDEX "IX_LegalDocuments_DocumentCode" ON "LegalDocuments" ("DocumentCode");

CREATE INDEX "IX_LegalDocuments_DocumentType" ON "LegalDocuments" ("DocumentType");

CREATE INDEX "IX_LegalDocuments_Status" ON "LegalDocuments" ("Status");

CREATE INDEX "IX_Notifications_UserId_CreatedAt" ON "Notifications" ("UserId", "CreatedAt");

CREATE INDEX "IX_Notifications_UserId_IsRead" ON "Notifications" ("UserId", "IsRead");

CREATE INDEX "IX_PaymentAccounts_BusinessId" ON "PaymentAccounts" ("BusinessId");

CREATE INDEX "IX_PaymentAccounts_BusinessId_IsDefault" ON "PaymentAccounts" ("BusinessId", "IsDefault");

CREATE INDEX "IX_Payments_PaidAt" ON "Payments" ("PaidAt");

CREATE INDEX "IX_Payments_PaymentAccountId" ON "Payments" ("PaymentAccountId");

CREATE INDEX "IX_Payments_TransactionId" ON "Payments" ("TransactionId");

CREATE INDEX "IX_PlanFeatures_SubscriptionPlanId" ON "PlanFeatures" ("SubscriptionPlanId");

CREATE INDEX "IX_ProductCategories_BusinessId" ON "ProductCategories" ("BusinessId");

CREATE INDEX "IX_ProductIngredients_IngredientId" ON "ProductIngredients" ("IngredientId");

CREATE INDEX "IX_ProductPrices_ApplyDate" ON "ProductPrices" ("ApplyDate");

CREATE INDEX "IX_ProductPrices_ProductId_ApplyDate" ON "ProductPrices" ("ProductId", "ApplyDate");

CREATE INDEX "IX_Products_BusinessCategoryId" ON "Products" ("BusinessCategoryId");

CREATE INDEX "IX_Products_BusinessId" ON "Products" ("BusinessId");

CREATE INDEX "IX_Products_BusinessId_BusinessCategoryId" ON "Products" ("BusinessId", "BusinessCategoryId");

CREATE INDEX "IX_Products_BusinessId_Status" ON "Products" ("BusinessId", "Status");

CREATE INDEX "IX_Products_Name" ON "Products" ("Name");

CREATE INDEX "IX_Products_ProductCategoryId" ON "Products" ("ProductCategoryId");

CREATE INDEX "IX_Suppliers_BusinessId" ON "Suppliers" ("BusinessId");

CREATE INDEX "IX_TaxCalculationLines_BusinessCategoryId" ON "TaxCalculationLines" ("BusinessCategoryId");

CREATE INDEX "IX_TaxCalculationLines_TaxCalculationId_SectionCode_IndicatorC~" ON "TaxCalculationLines" ("TaxCalculationId", "SectionCode", "IndicatorCode", "BusinessLocationId");

CREATE INDEX "IX_TaxCalculations_TaxPeriodId_IsCurrent" ON "TaxCalculations" ("TaxPeriodId", "IsCurrent");

CREATE UNIQUE INDEX "IX_TaxCalculations_TaxPeriodId_Version" ON "TaxCalculations" ("TaxPeriodId", "Version");

CREATE INDEX "IX_TaxDeclarationLines_TaxDeclarationId" ON "TaxDeclarationLines" ("TaxDeclarationId");

CREATE INDEX "IX_TaxDeclarationObligations_TaxDeclarationId" ON "TaxDeclarationObligations" ("TaxDeclarationId");

CREATE UNIQUE INDEX "IX_TaxDeclarations_DeclarationCode" ON "TaxDeclarations" ("DeclarationCode");

CREATE INDEX "IX_TaxDeclarations_TaxCalculationId" ON "TaxDeclarations" ("TaxCalculationId");

CREATE INDEX "IX_TaxDeclarations_TaxPeriodId_IsCurrent" ON "TaxDeclarations" ("TaxPeriodId", "IsCurrent");

CREATE UNIQUE INDEX "IX_TaxDeclarations_TaxPeriodId_Version" ON "TaxDeclarations" ("TaxPeriodId", "Version");

CREATE INDEX "IX_TaxPayments_TaxDeclarationId" ON "TaxPayments" ("TaxDeclarationId");

CREATE INDEX "IX_TaxPayments_TaxPeriodId" ON "TaxPayments" ("TaxPeriodId");

CREATE UNIQUE INDEX "IX_TaxPeriods_BusinessId_PeriodType_Year_Month_Quarter" ON "TaxPeriods" ("BusinessId", "PeriodType", "Year", "Month", "Quarter");

CREATE INDEX "IX_TaxPeriods_BusinessId_Status" ON "TaxPeriods" ("BusinessId", "Status");

CREATE INDEX "IX_TaxPeriods_BusinessId_Year_Month_Quarter" ON "TaxPeriods" ("BusinessId", "Year", "Month", "Quarter");

CREATE INDEX "IX_TaxPeriods_BusinessProfileId" ON "TaxPeriods" ("BusinessProfileId");

CREATE INDEX "IX_TaxPeriods_DueDate" ON "TaxPeriods" ("DueDate");

CREATE INDEX "IX_TaxPeriods_Status" ON "TaxPeriods" ("Status");

CREATE INDEX "IX_TransactionItems_ProductId" ON "TransactionItems" ("ProductId");

CREATE INDEX "IX_TransactionItems_TransactionId" ON "TransactionItems" ("TransactionId");

CREATE INDEX "IX_Transactions_BusinessId" ON "Transactions" ("BusinessId");

CREATE INDEX "IX_Transactions_InvoiceId" ON "Transactions" ("InvoiceId");

CREATE UNIQUE INDEX "IX_Transactions_TransactionCode" ON "Transactions" ("TransactionCode");

CREATE INDEX "IX_Transactions_TransactionDate" ON "Transactions" ("TransactionDate");

CREATE INDEX "IX_Transactions_TransactionDate_Status" ON "Transactions" ("TransactionDate", "Status");

CREATE INDEX "IX_UserDevices_UserId" ON "UserDevices" ("UserId");

CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

CREATE UNIQUE INDEX "IX_Users_GoogleId" ON "Users" ("GoogleId") WHERE "GoogleId" IS NOT NULL;

CREATE UNIQUE INDEX "IX_Users_Phone" ON "Users" ("Phone") WHERE "Phone" IS NOT NULL;

CREATE UNIQUE INDEX "IX_Users_TaxCode" ON "Users" ("TaxCode") WHERE "TaxCode" IS NOT NULL;

CREATE INDEX "IX_UserSubscriptions_EndDate" ON "UserSubscriptions" ("EndDate");

CREATE UNIQUE INDEX "IX_UserSubscriptions_PaymentOrderCode" ON "UserSubscriptions" ("PaymentOrderCode") WHERE "PaymentOrderCode" IS NOT NULL;

CREATE INDEX "IX_UserSubscriptions_Status" ON "UserSubscriptions" ("Status");

CREATE INDEX "IX_UserSubscriptions_SubscriptionPlanId" ON "UserSubscriptions" ("SubscriptionPlanId");

CREATE INDEX "IX_UserSubscriptions_UserId_SubscriptionPlanId_Status" ON "UserSubscriptions" ("UserId", "SubscriptionPlanId", "Status");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260817082340_InitialMigrate', '10.0.8');

COMMIT;

START TRANSACTION;
ALTER TABLE "Products"
    ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "BusinessProfiles"
    ADD COLUMN IF NOT EXISTS "IsStockTrackingEnabled" boolean NOT NULL DEFAULT FALSE;

UPDATE "BusinessCategories" SET "Name" = 'Dịch vụ'
WHERE "BusinessCategoryId" = 'a0000001-0000-4000-8000-000000000003';

UPDATE "BusinessCategories" SET "Name" = 'FNB'
WHERE "BusinessCategoryId" = 'd1111111-1111-1111-1111-111111111111';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260817083200_UpdateCategoryNames', '10.0.8');

COMMIT;

START TRANSACTION;
CREATE TABLE "RevenueThresholdAlerts" (
    "Id" uuid NOT NULL,
    "OwnerId" uuid NOT NULL,
    "Year" integer NOT NULL,
    "Quarter" integer NOT NULL,
    "WindowStart" timestamp without time zone NOT NULL,
    "WindowEnd" timestamp without time zone NOT NULL,
    "TotalRevenue" numeric(18,2) NOT NULL,
    "SentAt" timestamp without time zone NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_RevenueThresholdAlerts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RevenueThresholdAlerts_Users_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_RevenueThresholdAlerts_OwnerId_Year" ON "RevenueThresholdAlerts" ("OwnerId", "Year");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260817143600_AddRevenueThresholdAlerts', '10.0.8');

COMMIT;

START TRANSACTION;
CREATE TABLE "TaxPolicySettings" (
    "Id" uuid NOT NULL,
    "Year" integer NOT NULL,
    "AnnualRevenueThreshold" numeric(18,2) NOT NULL,
    "EInvoiceRevenueThreshold" numeric(18,2) NOT NULL,
    "UpdatedByUserId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_TaxPolicySettings" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_TaxPolicySettings_Year" ON "TaxPolicySettings" ("Year");

INSERT INTO "TaxPolicySettings"
(
    "Id", "Year", "AnnualRevenueThreshold",
    "EInvoiceRevenueThreshold", "UpdatedByUserId",
    "CreatedAt", "UpdatedAt"
)
VALUES
(
    '20260000-0000-4000-a000-000000000001',
    2026,
    1000000000,
    1000000000,
    NULL,
    TIMESTAMP '2026-08-20 00:00:00',
    TIMESTAMP '2026-08-20 00:00:00'
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260820153000_AddTaxPolicySettings', '10.0.8');

COMMIT;

START TRANSACTION;
CREATE TABLE "TaxThresholdSettings" (
    "Id" uuid NOT NULL,
    "Type" character varying(50) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "EffectiveFrom" date NOT NULL,
    "UpdatedByUserId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_TaxThresholdSettings" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_TaxThresholdSettings_Type_EffectiveFrom" ON "TaxThresholdSettings" ("Type", "EffectiveFrom");

INSERT INTO "TaxThresholdSettings"
(
    "Id", "Type", "Amount", "EffectiveFrom",
    "UpdatedByUserId", "CreatedAt", "UpdatedAt"
)
SELECT
    gen_random_uuid(),
    'AnnualRevenueTax',
    "AnnualRevenueThreshold",
    make_date("Year", 1, 1),
    "UpdatedByUserId",
    "CreatedAt",
    "UpdatedAt"
FROM "TaxPolicySettings"
UNION ALL
SELECT
    gen_random_uuid(),
    'EInvoiceRequirement',
    "EInvoiceRevenueThreshold",
    make_date("Year", 1, 1),
    "UpdatedByUserId",
    "CreatedAt",
    "UpdatedAt"
FROM "TaxPolicySettings";

DROP TABLE "TaxPolicySettings";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260820170000_UseEffectiveDatedTaxThresholds', '10.0.8');

COMMIT;

START TRANSACTION;
UPDATE "TaxThresholdSettings"
SET "Id" = '20260000-0000-4000-a000-000000000011'
WHERE "Type" = 'AnnualRevenueTax'
  AND "EffectiveFrom" = DATE '2026-01-01'
  AND NOT EXISTS
  (
      SELECT 1
      FROM "TaxThresholdSettings"
      WHERE "Id" =
          '20260000-0000-4000-a000-000000000011'
  );

UPDATE "TaxThresholdSettings"
SET "Id" = '20260000-0000-4000-a000-000000000012'
WHERE "Type" = 'EInvoiceRequirement'
  AND "EffectiveFrom" = DATE '2026-01-01'
  AND NOT EXISTS
  (
      SELECT 1
      FROM "TaxThresholdSettings"
      WHERE "Id" =
          '20260000-0000-4000-a000-000000000012'
  );

INSERT INTO "TaxThresholdSettings"
(
    "Id", "Type", "Amount", "EffectiveFrom",
    "UpdatedByUserId", "CreatedAt", "UpdatedAt"
)
VALUES
(
    '20260000-0000-4000-a000-000000000011',
    'AnnualRevenueTax',
    1000000000,
    DATE '2026-01-01',
    NULL,
    TIMESTAMP '2026-01-01 00:00:00',
    TIMESTAMP '2026-01-01 00:00:00'
),
(
    '20260000-0000-4000-a000-000000000012',
    'EInvoiceRequirement',
    1000000000,
    DATE '2026-01-01',
    NULL,
    TIMESTAMP '2026-01-01 00:00:00',
    TIMESTAMP '2026-01-01 00:00:00'
)
ON CONFLICT ("Type", "EffectiveFrom") DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260820173000_SeedDefaultTaxThresholdSettings', '10.0.8');

COMMIT;

START TRANSACTION;
DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM "TaxPeriods"
        WHERE "BusinessProfileId" IS NOT NULL
          AND "BusinessProfileId" <> "BusinessId"
    ) THEN
        RAISE EXCEPTION
            'TaxPeriods contains mismatched BusinessId/BusinessProfileId values; review before M0.';
    END IF;

    IF EXISTS
    (
        SELECT 1
        FROM "TaxPeriods"
        GROUP BY "BusinessId", "Year",
            CASE WHEN "PeriodType" = 'Monthly' THEN "Month" END,
            CASE WHEN "PeriodType" = 'Quarterly' THEN "Quarter" END,
            "PeriodType"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'TaxPeriods contains duplicate period identities; review before M0.';
    END IF;

    IF EXISTS
    (
        SELECT 1
        FROM "TaxPeriods"
        WHERE NOT
        (
            ("PeriodType" = 'Monthly'
                AND "Month" BETWEEN 1 AND 12
                AND "Quarter" IS NULL)
            OR ("PeriodType" = 'Quarterly'
                AND "Month" IS NULL
                AND "Quarter" BETWEEN 1 AND 4)
            OR ("PeriodType" = 'Yearly'
                AND "Month" IS NULL
                AND "Quarter" IS NULL)
        )
    ) THEN
        RAISE EXCEPTION
            'TaxPeriods contains invalid period shapes; review before M0.';
    END IF;
END $$;

ALTER TABLE "TaxPeriods" DROP CONSTRAINT "FK_TaxPeriods_BusinessProfiles_BusinessProfileId";

DROP INDEX "IX_TaxPeriods_BusinessId_PeriodType_Year_Month_Quarter";

DROP INDEX "IX_TaxPeriods_BusinessId_Year_Month_Quarter";

DROP INDEX "IX_TaxPeriods_BusinessProfileId";

ALTER TABLE "TaxPeriods" DROP COLUMN "BusinessProfileId";

CREATE UNIQUE INDEX "IX_TaxPeriods_BusinessId_Year" ON "TaxPeriods" ("BusinessId", "Year") WHERE "PeriodType" = 'Yearly';

CREATE UNIQUE INDEX "IX_TaxPeriods_BusinessId_Year_Month" ON "TaxPeriods" ("BusinessId", "Year", "Month") WHERE "PeriodType" = 'Monthly';

CREATE UNIQUE INDEX "IX_TaxPeriods_BusinessId_Year_Quarter" ON "TaxPeriods" ("BusinessId", "Year", "Quarter") WHERE "PeriodType" = 'Quarterly';

ALTER TABLE "TaxPeriods" ADD CONSTRAINT "CK_TaxPeriods_PeriodShape" CHECK (("PeriodType" = 'Monthly' AND "Month" BETWEEN 1 AND 12 AND "Quarter" IS NULL) OR ("PeriodType" = 'Quarterly' AND "Month" IS NULL AND "Quarter" BETWEEN 1 AND 4) OR ("PeriodType" = 'Yearly' AND "Month" IS NULL AND "Quarter" IS NULL));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260824110513_M0RepairTaxPeriodSchema', '10.0.8');

COMMIT;

START TRANSACTION;
ALTER TABLE "Transactions" ADD "CompletedAt" timestamp without time zone;

ALTER TABLE "TaxPeriods" ADD "EvidenceReviewedAt" timestamp without time zone;

ALTER TABLE "TaxPeriods" ADD "EvidenceReviewedByUserId" uuid;

ALTER TABLE "IngredientPurchases" ADD "ExpenseId" uuid;

ALTER TABLE "Incomes" ADD "AccountingType" character varying(30);

ALTER TABLE "Incomes" ADD "TransactionId" uuid;

ALTER TABLE "Expenses" ADD "VoucherNumber" character varying(100);

ALTER TABLE "ExpenseCategories" ADD "S2cGroupCode" character varying(30);

UPDATE "Transactions" AS t
SET "CompletedAt" = paid."CompletedAt"
FROM
(
    SELECT "TransactionId", MAX("PaidAt") AS "CompletedAt"
    FROM "Payments"
    WHERE "PaidAt" IS NOT NULL
    GROUP BY "TransactionId"
) paid
WHERE t."TransactionId" = paid."TransactionId"
  AND t."Status" = 'Completed'
  AND t."CompletedAt" IS NULL;

UPDATE "Expenses"
SET "VoucherNumber" =
    'PC-LEGACY-' || "ExpenseId"::text
WHERE "VoucherNumber" IS NULL;

ALTER TABLE "Expenses" ALTER COLUMN "VoucherNumber" SET NOT NULL;

CREATE INDEX "IX_Transactions_BusinessId_CompletedAt" ON "Transactions" ("BusinessId", "CompletedAt");

CREATE INDEX "IX_TaxPeriods_EvidenceReviewedByUserId" ON "TaxPeriods" ("EvidenceReviewedByUserId");

ALTER TABLE "TaxPeriods" ADD CONSTRAINT "CK_TaxPeriods_EvidenceReviewPair" CHECK (("EvidenceReviewedAt" IS NULL AND "EvidenceReviewedByUserId" IS NULL) OR ("EvidenceReviewedAt" IS NOT NULL AND "EvidenceReviewedByUserId" IS NOT NULL));

CREATE INDEX "IX_IngredientPurchases_ExpenseId" ON "IngredientPurchases" ("ExpenseId");

CREATE UNIQUE INDEX "IX_Incomes_TransactionId" ON "Incomes" ("TransactionId") WHERE "TransactionId" IS NOT NULL;

ALTER TABLE "Incomes" ADD CONSTRAINT "CK_Incomes_AccountingType" CHECK ("AccountingType" IS NULL OR "AccountingType" IN ('BusinessRevenue', 'NonRevenueCashIn'));

CREATE UNIQUE INDEX "IX_Expenses_BusinessId_VoucherNumber" ON "Expenses" ("BusinessId", "VoucherNumber");

ALTER TABLE "ExpenseCategories" ADD CONSTRAINT "CK_ExpenseCategories_S2cGroupCode" CHECK ("S2cGroupCode" IS NULL OR "S2cGroupCode" IN ('Labor', 'PurchasedServices', 'OtherDirect'));

ALTER TABLE "Incomes" ADD CONSTRAINT "FK_Incomes_Transactions_TransactionId" FOREIGN KEY ("TransactionId") REFERENCES "Transactions" ("TransactionId") ON DELETE RESTRICT;

ALTER TABLE "IngredientPurchases" ADD CONSTRAINT "FK_IngredientPurchases_Expenses_ExpenseId" FOREIGN KEY ("ExpenseId") REFERENCES "Expenses" ("ExpenseId") ON DELETE RESTRICT;

ALTER TABLE "TaxPeriods" ADD CONSTRAINT "FK_TaxPeriods_Users_EvidenceReviewedByUserId" FOREIGN KEY ("EvidenceReviewedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260824110644_M1AddAccountingSourceMetadata', '10.0.8');

COMMIT;

START TRANSACTION;
DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM "TaxCalculations"
        WHERE "IsCurrent" = TRUE
        GROUP BY "TaxPeriodId", "RecommendedFormCode"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'Multiple current calculations exist for the same period/form; review before M2.';
    END IF;

    IF EXISTS
    (
        SELECT 1
        FROM "TaxDeclarations"
        WHERE "IsCurrent" = TRUE
        GROUP BY "TaxPeriodId", "FormCode"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'Multiple current declarations exist for the same period/form; review before M2.';
    END IF;
END $$;

DROP INDEX "IX_TaxDeclarations_TaxPeriodId_IsCurrent";

DROP INDEX "IX_TaxDeclarations_TaxPeriodId_Version";

DROP INDEX "IX_TaxCalculations_TaxPeriodId_IsCurrent";

DROP INDEX "IX_TaxCalculations_TaxPeriodId_Version";

DROP INDEX "IX_RevenueThresholdAlerts_OwnerId_Year";

ALTER TABLE "Users" ADD "CommencementPeriod" character varying(30);

ALTER TABLE "Users" ADD "CommencementTaxYear" integer;

ALTER TABLE "Users" ADD "DeclaredRevenueBracket" character varying(30);

ALTER TABLE "Users" ADD "PersonalIncomeTaxMethod" character varying(30);

ALTER TABLE "Users" ADD "TaxMethodEffectiveYear" integer;

ALTER TABLE "Users" ADD "TaxProfileConfirmedAt" timestamp without time zone;

ALTER TABLE "TaxPayments" ADD "TaxType" character varying(30);

ALTER TABLE "TaxDeclarations" ADD "FormDataJson" jsonb;

ALTER TABLE "TaxCalculations" ADD "TaxMethod" character varying(30);

ALTER TABLE "TaxCalculations" ADD "TaxMethodEffectiveYear" integer;

ALTER TABLE "RevenueThresholdAlerts" ADD "ResolvedAt" timestamp without time zone;

ALTER TABLE "RevenueThresholdAlerts" ADD "Status" character varying(30);

ALTER TABLE "RevenueThresholdAlerts" ADD "ThresholdAmount" numeric(18,2);

ALTER TABLE "RevenueThresholdAlerts" ADD "ThresholdCode" character varying(30);

UPDATE "TaxPayments"
SET "TaxType" = 'Unknown'
WHERE "TaxType" IS NULL;

UPDATE "TaxCalculations"
SET "TaxMethod" = 'RevenueBased'
WHERE "TaxMethod" IS NULL;

UPDATE "RevenueThresholdAlerts" alert
SET
    "ThresholdCode" = 'Crossed1B',
    "ThresholdAmount" = COALESCE
    (
        (
            SELECT setting."Amount"
            FROM "TaxThresholdSettings" setting
            WHERE setting."Type" = 'AnnualRevenueTax'
              AND setting."EffectiveFrom" <= make_date(alert."Year", 12, 31)
            ORDER BY setting."EffectiveFrom" DESC
            LIMIT 1
        ),
        1000000000
    ),
    "Status" = 'PendingReview',
    "ResolvedAt" = NULL
WHERE "ThresholdCode" IS NULL;

ALTER TABLE "TaxPayments" ALTER COLUMN "TaxType" SET NOT NULL;

ALTER TABLE "TaxCalculations" ALTER COLUMN "TaxMethod" SET NOT NULL;

ALTER TABLE "RevenueThresholdAlerts" ALTER COLUMN "Status" SET NOT NULL;

ALTER TABLE "RevenueThresholdAlerts" ALTER COLUMN "ThresholdAmount" SET NOT NULL;

ALTER TABLE "RevenueThresholdAlerts" ALTER COLUMN "ThresholdCode" SET NOT NULL;

INSERT INTO "TaxThresholdSettings" ("Id", "Amount", "CreatedAt", "EffectiveFrom", "Type", "UpdatedAt", "UpdatedByUserId")
VALUES ('20260000-0000-4000-a000-000000000013', 3000000000.0, TIMESTAMP '2026-01-01T00:00:00', DATE '2026-01-01', 'IncomeBasedRequirement', TIMESTAMP '2026-01-01T00:00:00', NULL);
INSERT INTO "TaxThresholdSettings" ("Id", "Amount", "CreatedAt", "EffectiveFrom", "Type", "UpdatedAt", "UpdatedByUserId")
VALUES ('20260000-0000-4000-a000-000000000014', 50000000000.0, TIMESTAMP '2026-01-01T00:00:00', DATE '2026-01-01', 'SupportedRevenueCeiling', TIMESTAMP '2026-01-01T00:00:00', NULL);

ALTER TABLE "Users" ADD CONSTRAINT "CK_Users_CommencementPair" CHECK (("CommencementPeriod" IS NULL AND "CommencementTaxYear" IS NULL) OR ("CommencementPeriod" IS NOT NULL AND "CommencementTaxYear" IS NOT NULL));

ALTER TABLE "Users" ADD CONSTRAINT "CK_Users_CommencementPeriod" CHECK ("CommencementPeriod" IS NULL OR "CommencementPeriod" IN ('BeforeTaxYear', 'FirstHalfOfTaxYear', 'SecondHalfOfTaxYear'));

ALTER TABLE "Users" ADD CONSTRAINT "CK_Users_DeclaredRevenueBracket" CHECK ("DeclaredRevenueBracket" IS NULL OR "DeclaredRevenueBracket" IN ('AtOrBelow1B', 'Over1BTo3B', 'Over3BTo50B'));

ALTER TABLE "Users" ADD CONSTRAINT "CK_Users_PersonalIncomeTaxMethod" CHECK ("PersonalIncomeTaxMethod" IS NULL OR "PersonalIncomeTaxMethod" IN ('RevenueBased', 'IncomeBased'));

ALTER TABLE "Users" ADD CONSTRAINT "CK_Users_TaxMethodPair" CHECK (("PersonalIncomeTaxMethod" IS NULL AND "TaxMethodEffectiveYear" IS NULL) OR ("PersonalIncomeTaxMethod" IS NOT NULL AND "TaxMethodEffectiveYear" IS NOT NULL));

ALTER TABLE "Users" ADD CONSTRAINT "CK_Users_TaxProfileCompatibility" CHECK (("DeclaredRevenueBracket" IS NULL AND "PersonalIncomeTaxMethod" IS NULL AND "TaxMethodEffectiveYear" IS NULL AND "CommencementPeriod" IS NULL AND "CommencementTaxYear" IS NULL AND "TaxProfileConfirmedAt" IS NULL) OR ("DeclaredRevenueBracket" = 'AtOrBelow1B' AND "PersonalIncomeTaxMethod" IS NULL) OR ("DeclaredRevenueBracket" = 'Over1BTo3B' AND "PersonalIncomeTaxMethod" IN ('RevenueBased', 'IncomeBased') AND "CommencementPeriod" IS NULL) OR ("DeclaredRevenueBracket" = 'Over3BTo50B' AND "PersonalIncomeTaxMethod" = 'IncomeBased' AND "CommencementPeriod" IS NULL));

CREATE INDEX "IX_TaxPayments_TaxType_Status_PaymentDate" ON "TaxPayments" ("TaxType", "Status", "PaymentDate");

ALTER TABLE "TaxPayments" ADD CONSTRAINT "CK_TaxPayments_TaxType" CHECK ("TaxType" IN ('VAT', 'PIT', 'Unknown'));

CREATE UNIQUE INDEX "IX_TaxDeclarations_TaxPeriodId_FormCode" ON "TaxDeclarations" ("TaxPeriodId", "FormCode") WHERE "IsCurrent" = TRUE;

CREATE UNIQUE INDEX "IX_TaxDeclarations_TaxPeriodId_FormCode_Version" ON "TaxDeclarations" ("TaxPeriodId", "FormCode", "Version");

CREATE UNIQUE INDEX "IX_TaxCalculations_TaxPeriodId_RecommendedFormCode" ON "TaxCalculations" ("TaxPeriodId", "RecommendedFormCode") WHERE "IsCurrent" = TRUE;

CREATE UNIQUE INDEX "IX_TaxCalculations_TaxPeriodId_RecommendedFormCode_Version" ON "TaxCalculations" ("TaxPeriodId", "RecommendedFormCode", "Version");

ALTER TABLE "TaxCalculations" ADD CONSTRAINT "CK_TaxCalculations_TaxMethod" CHECK ("TaxMethod" IN ('RevenueBased', 'IncomeBased'));

CREATE INDEX "IX_RevenueThresholdAlerts_OwnerId_Status" ON "RevenueThresholdAlerts" ("OwnerId", "Status");

CREATE UNIQUE INDEX "IX_RevenueThresholdAlerts_OwnerId_Year_ThresholdCode" ON "RevenueThresholdAlerts" ("OwnerId", "Year", "ThresholdCode");

ALTER TABLE "RevenueThresholdAlerts" ADD CONSTRAINT "CK_RevenueThresholdAlerts_Amount" CHECK ("ThresholdAmount" > 0);

ALTER TABLE "RevenueThresholdAlerts" ADD CONSTRAINT "CK_RevenueThresholdAlerts_Code" CHECK ("ThresholdCode" IN ('Crossed1B', 'Crossed3B', 'Crossed50B'));

ALTER TABLE "RevenueThresholdAlerts" ADD CONSTRAINT "CK_RevenueThresholdAlerts_Resolution" CHECK (("Status" = 'Resolved' AND "ResolvedAt" IS NOT NULL) OR ("Status" <> 'Resolved' AND "ResolvedAt" IS NULL));

ALTER TABLE "RevenueThresholdAlerts" ADD CONSTRAINT "CK_RevenueThresholdAlerts_Status" CHECK ("Status" IN ('PendingReview', 'Acknowledged', 'Resolved'));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260824111047_M2AddTaxProfileAndArtifactIdentity', '10.0.8');

COMMIT;

START TRANSACTION;
DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1 FROM "Products"
        WHERE ABS("StockQuantity") >= 1000000000000
    ) OR EXISTS
    (
        SELECT 1 FROM "Ingredients"
        WHERE ABS("StockQuantity") >= 1000000000000
    ) THEN
        RAISE EXCEPTION
            'StockQuantity exceeds numeric(18,6) range; review before M3.';
    END IF;
END $$;

ALTER TABLE "Products" ALTER COLUMN "StockQuantity" TYPE numeric(18,6);

ALTER TABLE "Ingredients" ALTER COLUMN "StockQuantity" TYPE numeric(18,6);

CREATE TABLE "InventoryMovements" (
    "InventoryMovementId" uuid NOT NULL,
    "BusinessId" uuid NOT NULL,
    "ProductId" uuid,
    "IngredientId" uuid,
    "MovementType" character varying(30) NOT NULL,
    "Quantity" numeric(18,6) NOT NULL,
    "TotalValue" numeric(20,2),
    "OccurredAt" timestamp without time zone NOT NULL,
    "DocumentNumber" character varying(100) NOT NULL,
    "Description" character varying(1000) NOT NULL,
    "ReferenceId" uuid,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_InventoryMovements" PRIMARY KEY ("InventoryMovementId"),
    CONSTRAINT "CK_InventoryMovements_ExactlyOneItem" CHECK (("ProductId" IS NOT NULL AND "IngredientId" IS NULL) OR ("ProductId" IS NULL AND "IngredientId" IS NOT NULL)),
    CONSTRAINT "CK_InventoryMovements_QuantityPositive" CHECK ("Quantity" > 0),
    CONSTRAINT "CK_InventoryMovements_ReferenceShape" CHECK (("MovementType" IN ('PurchaseIn', 'OrderOut') AND "ReferenceId" IS NOT NULL) OR ("MovementType" IN ('OpeningBalance', 'AdjustmentIn', 'AdjustmentOut') AND "ReferenceId" IS NULL)),
    CONSTRAINT "CK_InventoryMovements_TotalValueNonNegative" CHECK ("TotalValue" IS NULL OR "TotalValue" >= 0),
    CONSTRAINT "CK_InventoryMovements_Type" CHECK ("MovementType" IN ('OpeningBalance', 'PurchaseIn', 'OrderOut', 'AdjustmentIn', 'AdjustmentOut')),
    CONSTRAINT "FK_InventoryMovements_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_InventoryMovements_Ingredients_IngredientId" FOREIGN KEY ("IngredientId") REFERENCES "Ingredients" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_InventoryMovements_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_InventoryMovements_BusinessId_OccurredAt" ON "InventoryMovements" ("BusinessId", "OccurredAt");

CREATE UNIQUE INDEX "IX_InventoryMovements_IngredientId" ON "InventoryMovements" ("IngredientId") WHERE "MovementType" = 'OpeningBalance' AND "IngredientId" IS NOT NULL;

CREATE INDEX "IX_InventoryMovements_IngredientId_OccurredAt" ON "InventoryMovements" ("IngredientId", "OccurredAt") WHERE "IngredientId" IS NOT NULL;

CREATE INDEX "IX_InventoryMovements_MovementType_ReferenceId" ON "InventoryMovements" ("MovementType", "ReferenceId");

CREATE UNIQUE INDEX "IX_InventoryMovements_ProductId" ON "InventoryMovements" ("ProductId") WHERE "MovementType" = 'OpeningBalance' AND "ProductId" IS NOT NULL;

CREATE INDEX "IX_InventoryMovements_ProductId_OccurredAt" ON "InventoryMovements" ("ProductId", "OccurredAt") WHERE "ProductId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260824111235_M3AddInventoryLedger', '10.0.8');

COMMIT;

START TRANSACTION;
ALTER TABLE "PaymentAccounts" DROP CONSTRAINT "FK_PaymentAccounts_BusinessProfiles_BusinessId";

ALTER TABLE "Payments" DROP CONSTRAINT "FK_Payments_PaymentAccounts_PaymentAccountId";

ALTER TABLE "PaymentAccounts" ALTER COLUMN "BankShortName" DROP NOT NULL;

ALTER TABLE "PaymentAccounts" ALTER COLUMN "BankName" DROP NOT NULL;

ALTER TABLE "PaymentAccounts" ALTER COLUMN "AccountNumber" DROP NOT NULL;

ALTER TABLE "PaymentAccounts" ALTER COLUMN "AccountName" DROP NOT NULL;

ALTER TABLE "PaymentAccounts" ADD "AccountType" character varying(20);

ALTER TABLE "PaymentAccounts" ADD "InitialBalance" numeric(20,2);

ALTER TABLE "PaymentAccounts" ADD "InitialBalanceDate" date;

ALTER TABLE "PaymentAccounts" ADD "IsActive" boolean;

UPDATE "PaymentAccounts"
SET
    "AccountType" = 'Bank',
    "IsActive" = TRUE
WHERE "AccountType" IS NULL;

DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM "PaymentAccounts"
        WHERE "AccountType" = 'Bank'
          AND "IsDefault" = TRUE
          AND "IsActive" = TRUE
        GROUP BY "BusinessId"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'Multiple active default bank accounts exist for a business; review before M4.';
    END IF;
END $$;

INSERT INTO "PaymentAccounts"
(
    "PaymentAccountId", "BusinessId", "IsDefault",
    "Description", "AccountType", "IsActive",
    "CreatedAt", "UpdatedAt"
)
SELECT
    gen_random_uuid(),
    business."Id",
    FALSE,
    'Tiền mặt',
    'Cash',
    TRUE,
    (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
    (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
FROM "BusinessProfiles" business
WHERE NOT EXISTS
(
    SELECT 1
    FROM "PaymentAccounts" account
    WHERE account."BusinessId" = business."Id"
      AND account."AccountType" = 'Cash'
);

UPDATE "Payments" payment
SET "PaymentAccountId" = cash."PaymentAccountId"
FROM "Transactions" AS t, "PaymentAccounts" AS cash
WHERE payment."TransactionId" = t."TransactionId"
  AND cash."BusinessId" = t."BusinessId"
  AND cash."AccountType" = 'Cash'
  AND payment."PaymentAccountId" IS NULL
  AND LOWER(BTRIM(payment."PaymentMethod")) = 'cash';

ALTER TABLE "PaymentAccounts" ALTER COLUMN "AccountType" SET NOT NULL;

ALTER TABLE "PaymentAccounts" ALTER COLUMN "IsActive" SET NOT NULL;

CREATE TABLE "MoneyMovements" (
    "MoneyMovementId" uuid NOT NULL,
    "PaymentAccountId" uuid NOT NULL,
    "MovementType" character varying(30) NOT NULL,
    "Amount" numeric(20,2) NOT NULL,
    "MovementDate" timestamp without time zone NOT NULL,
    "DocumentNumber" character varying(100) NOT NULL,
    "Description" character varying(1000) NOT NULL,
    "ReferenceId" uuid NOT NULL,
    "CreatedAt" timestamp without time zone NOT NULL,
    "UpdatedAt" timestamp without time zone NOT NULL,
    CONSTRAINT "PK_MoneyMovements" PRIMARY KEY ("MoneyMovementId"),
    CONSTRAINT "CK_MoneyMovements_AmountPositive" CHECK ("Amount" > 0),
    CONSTRAINT "CK_MoneyMovements_Type" CHECK ("MovementType" IN ('PaymentIn', 'ManualIncomeIn', 'ExpenseOut')),
    CONSTRAINT "FK_MoneyMovements_PaymentAccounts_PaymentAccountId" FOREIGN KEY ("PaymentAccountId") REFERENCES "PaymentAccounts" ("PaymentAccountId") ON DELETE RESTRICT
);

CREATE INDEX "IX_PaymentAccounts_BusinessId_AccountType_IsActive" ON "PaymentAccounts" ("BusinessId", "AccountType", "IsActive");

CREATE UNIQUE INDEX "IX_PaymentAccounts_OneActiveDefaultBank" ON "PaymentAccounts" ("BusinessId") WHERE "AccountType" = 'Bank' AND "IsDefault" = TRUE AND "IsActive" = TRUE;

CREATE UNIQUE INDEX "IX_PaymentAccounts_OneCashAccount" ON "PaymentAccounts" ("BusinessId") WHERE "AccountType" = 'Cash';

ALTER TABLE "PaymentAccounts" ADD CONSTRAINT "CK_PaymentAccounts_AccountType" CHECK ("AccountType" IN ('Cash', 'Bank'));

ALTER TABLE "PaymentAccounts" ADD CONSTRAINT "CK_PaymentAccounts_BankFields" CHECK ("AccountType" <> 'Bank' OR ("BankShortName" IS NOT NULL AND "BankName" IS NOT NULL AND "AccountNumber" IS NOT NULL AND "AccountName" IS NOT NULL));

ALTER TABLE "PaymentAccounts" ADD CONSTRAINT "CK_PaymentAccounts_InitialBalancePair" CHECK (("InitialBalance" IS NULL AND "InitialBalanceDate" IS NULL) OR ("InitialBalance" IS NOT NULL AND "InitialBalanceDate" IS NOT NULL));

CREATE UNIQUE INDEX "IX_MoneyMovements_MovementType_ReferenceId" ON "MoneyMovements" ("MovementType", "ReferenceId");

CREATE INDEX "IX_MoneyMovements_PaymentAccountId_MovementDate" ON "MoneyMovements" ("PaymentAccountId", "MovementDate");

ALTER TABLE "PaymentAccounts" ADD CONSTRAINT "FK_PaymentAccounts_BusinessProfiles_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES "BusinessProfiles" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Payments" ADD CONSTRAINT "FK_Payments_PaymentAccounts_PaymentAccountId" FOREIGN KEY ("PaymentAccountId") REFERENCES "PaymentAccounts" ("PaymentAccountId") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260824111447_M4AddCashBankMoneyLedger', '10.0.8');

COMMIT;

START TRANSACTION;
ALTER TABLE "TaxCalculations" ADD "ApplicablePersonalIncomeTaxRate" numeric(9,4) NOT NULL DEFAULT 0.0;

ALTER TABLE "TaxCalculations" ADD "CalculationDataJson" jsonb;

ALTER TABLE "TaxCalculations" ADD "TotalDeductibleExpenses" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE "TaxCalculations" ADD "TotalPitOverpaid" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE "TaxCalculations" ADD "TotalPitPaid" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE "TaxCalculations" ADD "TotalTaxableIncome" numeric(18,2) NOT NULL DEFAULT 0.0;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260824111623_M5AddQttCalculationSnapshots', '10.0.8');

COMMIT;

START TRANSACTION;
DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM "InventoryMovements"
        WHERE "ReferenceId" IS NOT NULL
          AND "ProductId" IS NOT NULL
        GROUP BY "MovementType", "ReferenceId", "ProductId"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'InventoryMovements contains duplicate product source lines; aggregate each MovementType/ReferenceId/ProductId before M6.';
    END IF;

    IF EXISTS
    (
        SELECT 1
        FROM "InventoryMovements"
        WHERE "ReferenceId" IS NOT NULL
          AND "IngredientId" IS NOT NULL
        GROUP BY "MovementType", "ReferenceId", "IngredientId"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'InventoryMovements contains duplicate ingredient source lines; aggregate each MovementType/ReferenceId/IngredientId before M6.';
    END IF;
END $$;

CREATE UNIQUE INDEX "IX_InventoryMovements_MovementType_ReferenceId_IngredientId" ON "InventoryMovements" ("MovementType", "ReferenceId", "IngredientId") WHERE "ReferenceId" IS NOT NULL AND "IngredientId" IS NOT NULL;

CREATE UNIQUE INDEX "IX_InventoryMovements_MovementType_ReferenceId_ProductId" ON "InventoryMovements" ("MovementType", "ReferenceId", "ProductId") WHERE "ReferenceId" IS NOT NULL AND "ProductId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260824114829_M6AddInventoryMovementSourceIdempotency', '10.0.8');

COMMIT;

START TRANSACTION;
DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM "TaxPeriods"
        WHERE "Year" NOT BETWEEN 2 AND 9998
           OR NOT
           (
               ("PeriodType" = 'Monthly'
                   AND "Month" BETWEEN 1 AND 12
                   AND "Quarter" IS NULL)
               OR ("PeriodType" = 'Quarterly'
                   AND "Month" IS NULL
                   AND "Quarter" BETWEEN 1 AND 4)
               OR ("PeriodType" = 'Yearly'
                   AND "Month" IS NULL
                   AND "Quarter" IS NULL)
           )
    ) THEN
        RAISE EXCEPTION
            'TaxPeriods contains an unsupported identity shape or year outside 2..9998; review before M7.';
    END IF;

    UPDATE "TaxPeriods"
    SET
        "PeriodStartDate" =
            CASE "PeriodType"
                WHEN 'Monthly' THEN
                    make_date("Year", "Month", 1)::timestamp
                        - INTERVAL '7 hours'
                WHEN 'Quarterly' THEN
                    make_date(
                        "Year",
                        (("Quarter" - 1) * 3) + 1,
                        1)::timestamp
                        - INTERVAL '7 hours'
                WHEN 'Yearly' THEN
                    make_date("Year", 1, 1)::timestamp
                        - INTERVAL '7 hours'
            END,
        "PeriodEndDate" =
            CASE "PeriodType"
                WHEN 'Monthly' THEN
                    make_date("Year", "Month", 1)::timestamp
                        + INTERVAL '1 month'
                        - INTERVAL '7 hours'
                WHEN 'Quarterly' THEN
                    make_date(
                        "Year",
                        (("Quarter" - 1) * 3) + 1,
                        1)::timestamp
                        + INTERVAL '3 months'
                        - INTERVAL '7 hours'
                WHEN 'Yearly' THEN
                    make_date("Year", 1, 1)::timestamp
                        + INTERVAL '1 year'
                        - INTERVAL '7 hours'
            END;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260824121306_M7NormalizeTaxPeriodBangkokBoundaries', '10.0.8');

COMMIT;

START TRANSACTION;
DO $$
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM "PaymentAccounts"
        WHERE "SePayBankAccountXid" IS NOT NULL
        GROUP BY "SePayBankAccountXid"
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'PaymentAccounts contains duplicate non-null SePayBankAccountXid values; resolve them before M8.';
    END IF;
END $$;

ALTER TABLE "BusinessProfiles" ADD "InventoryInitializedAt" timestamp without time zone;

CREATE UNIQUE INDEX "IX_PaymentAccounts_SePayBankAccountXid" ON "PaymentAccounts" ("SePayBankAccountXid") WHERE "SePayBankAccountXid" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260824152737_M8AddInventoryCutoverAndSePayXidUniqueness', '10.0.8');

COMMIT;

