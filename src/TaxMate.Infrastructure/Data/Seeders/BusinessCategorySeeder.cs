using Microsoft.EntityFrameworkCore;
using TaxMate.Model.Data;
using TaxMate.Model.Entities;

namespace TaxMate.Infrastructure.Data.Seeders;

public static class BusinessCategorySeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (!await context.BusinessCategories
                .AnyAsync(x => x.Code == "FNB"))
        {
            context.BusinessCategories.Add(new BusinessCategory
            {
                BusinessCategoryId = Guid.NewGuid(),
                Code = "FNB",
                Name = "Ăn uống, nhà hàng, F&B",
                Description = "Hoạt động dịch vụ ăn uống có gắn với hàng hóa.",

                VatRate = 3.00m,
                PitRate = 1.50m,

                FormSectionCode = "I",
                FormIndicatorCode = "08d",

                IsActive = true,

                EffectiveFrom = new DateTime(
                    2026, 1, 1,
                    0, 0, 0,
                    DateTimeKind.Utc)
            });
        }

        if (!await context.BusinessCategories
                .AnyAsync(x => x.Code == "SERVICE"))
        {
            context.BusinessCategories.Add(new BusinessCategory
            {
                BusinessCategoryId = Guid.NewGuid(),
                Code = "SERVICE",
                Name = "Dịch vụ",
                Description =
                    "Dịch vụ, xây dựng không bao thầu nguyên vật liệu.",

                VatRate = 5.00m,
                PitRate = 2.00m,

                FormSectionCode = "I",
                FormIndicatorCode = "08b",

                IsActive = true,

                EffectiveFrom = new DateTime(
                    2026, 1, 1,
                    0, 0, 0,
                    DateTimeKind.Utc)
            });
        }

        await context.SaveChangesAsync();
    }
}