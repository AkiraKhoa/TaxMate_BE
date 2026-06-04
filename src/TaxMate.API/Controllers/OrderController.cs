using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[Controller]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost("business/{businessId}")]
    public async Task<IActionResult> CreateOrder(Guid businessId, [FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var id = await _orderService.CreateOrderAsync(businessId, request);
        return Ok(id);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderDetail(Guid id)
    {
        var result = await _orderService.GetOrderDetailAsync(id);
        return Ok(result);
    }

    [HttpGet("business/{businessId}")]
    public async Task<IActionResult> GetOrders(Guid businessId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _orderService.GetOrdersByBusinessAsync(businessId, page, pageSize);
        return Ok(result);
    }

    [HttpPost("{id}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddOrderItemRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _orderService.AddItemAsync(id, request);
        return Ok();
    }

    [HttpPut("{id}/items/{itemId}")]
    public async Task<IActionResult> UpdateItem(Guid id, Guid itemId, [FromBody] UpdateOrderItemRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _orderService.UpdateItemAsync(id, itemId, request);
        return Ok();
    }

    [HttpDelete("{id}/items/{itemId}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId)
    {
        await _orderService.RemoveItemAsync(id, itemId);
        return Ok();
    }

    [HttpPost("{id}/discount")]
    public async Task<IActionResult> ApplyDiscount(Guid id, [FromBody] ApplyDiscountRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _orderService.ApplyDiscountAsync(id, request);
        return Ok();
    }

    [HttpDelete("{id}/discount")]
    public async Task<IActionResult> RemoveDiscount(Guid id)
    {
        await _orderService.RemoveDiscountAsync(id);
        return Ok();
    }

    [HttpPost("{id}/surcharge")]
    public async Task<IActionResult> ApplySurcharge(Guid id, [FromBody] ApplySurchargeRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _orderService.ApplySurchargeAsync(id, request);
        return Ok();
    }

    [HttpDelete("{id}/surcharge")]
    public async Task<IActionResult> RemoveSurcharge(Guid id)
    {
        await _orderService.RemoveSurchargeAsync(id);
        return Ok();
    }

    [HttpPost("{id}/checkout")]
    public async Task<IActionResult> Checkout(Guid id, [FromBody] CheckoutRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _orderService.CheckoutAsync(id, request);
        return Ok(result);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        await _orderService.CancelOrderAsync(id);
        return Ok();
    }
}
