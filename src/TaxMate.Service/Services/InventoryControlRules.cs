using TaxMate.Model.Common;
using TaxMate.Model.DTO.Inventory;
using TaxMate.Model.Entities;
using TaxMate.Service.Exceptions;

namespace TaxMate.Service.Services;

internal static class InventoryControlRules
{
    internal static string Version(IEnumerable<Product> products, IEnumerable<Ingredient> ingredients,
        IEnumerable<InventoryMovement> movements)
    {
        var snapshot = new
        {
            Items = MapItems(products, ingredients).OrderBy(x => x.ProductId).ThenBy(x => x.IngredientId),
            Movements = movements.OrderBy(x => x.InventoryMovementId).Select(x => new
            {
                x.InventoryMovementId, x.ProductId, x.IngredientId, x.Quantity,
                x.TotalValue, x.MovementType, x.OccurredAt
            })
        };
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(snapshot)));
    }
    internal static InventoryItemKey GetKey(Guid? productId, Guid? ingredientId)
    {
        if (productId.HasValue == ingredientId.HasValue ||
            productId == Guid.Empty || ingredientId == Guid.Empty)
        {
            throw new BadRequestException(
                "Mỗi dòng kiểm kho phải gắn với đúng một hàng hóa hoặc nguyên liệu.");
        }

        return new InventoryItemKey(productId, ingredientId);
    }

    internal static IReadOnlyDictionary<InventoryItemKey, decimal> CalculateQuantities(
        IEnumerable<InventoryMovement> movements)
    {
        var quantities = new Dictionary<InventoryItemKey, decimal>();
        foreach (var movement in movements)
        {
            var key = GetKey(movement.ProductId, movement.IngredientId);
            var direction = movement.MovementType switch
            {
                InventoryMovementTypes.OpeningBalance or
                InventoryMovementTypes.PurchaseIn or
                InventoryMovementTypes.AdjustmentIn => 1m,
                InventoryMovementTypes.OrderOut or
                InventoryMovementTypes.AdjustmentOut => -1m,
                _ => throw new ConflictException(
                    "Lịch sử kho có loại phát sinh không hợp lệ.")
            };
            quantities[key] = quantities.GetValueOrDefault(key) +
                              direction * movement.Quantity;
        }

        return quantities;
    }

    internal static IReadOnlyCollection<InventoryItemKey> ActiveKeys(
        IEnumerable<Product> products,
        IEnumerable<Ingredient> ingredients)
    {
        return products.Select(x => InventoryItemKey.ForProduct(x.Id))
            .Concat(ingredients.Select(x => InventoryItemKey.ForIngredient(x.Id)))
            .ToArray();
    }

    internal static void EnsureExactActiveItemSet(
        IReadOnlyCollection<InventoryItemKey> activeKeys,
        IReadOnlyCollection<InventoryItemKey> submittedKeys)
    {
        var duplicate = submittedKeys.GroupBy(x => x)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new BadRequestException("Một mặt hàng bị nhập kiểm kho nhiều lần.");
        }

        var active = activeKeys.ToHashSet();
        var submitted = submittedKeys.ToHashSet();
        if (!active.SetEquals(submitted))
        {
            throw new BadRequestException(
                "Phải xác nhận số lượng cho đúng và đủ tất cả mặt hàng đang hoạt động.");
        }
    }

    internal static IReadOnlyList<InventoryControlItemResponse> MapItems(
        IEnumerable<Product> products,
        IEnumerable<Ingredient> ingredients)
    {
        return products.Select(product => new InventoryControlItemResponse
            {
                ProductId = product.Id,
                Name = product.Name,
                Unit = product.Unit,
                CurrentQuantity = product.StockQuantity ?? 0m,
                CurrentUnitValue = product.CostPrice
            })
            .Concat(ingredients.Select(ingredient => new InventoryControlItemResponse
            {
                IngredientId = ingredient.Id,
                Name = ingredient.Name,
                Unit = ingredient.Unit,
                CurrentQuantity = ingredient.StockQuantity,
                CurrentUnitValue = ingredient.EstimatedPrice
            }))
            .OrderBy(x => x.Name)
            .ThenBy(x => x.ProductId ?? x.IngredientId)
            .ToArray();
    }
}
