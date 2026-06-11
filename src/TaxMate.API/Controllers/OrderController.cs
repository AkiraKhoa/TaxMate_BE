using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.Common;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

/// <summary>Quản lý đơn hàng POS (tạo, sửa item, checkout).</summary>
[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>Tạo đơn hàng nháp mới.</summary>
    /// <param name="businessId">ID cửa hàng. Chạy SeedTestData để lấy ID thật.</param>
    /// <param name="request">Thông tin đơn hàng.</param>
    [HttpPost("business/{businessId}")]
    public async Task<IActionResult> CreateOrder(Guid businessId, [FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var id = await _orderService.CreateOrderAsync(businessId, request);
        return Created(
            $"api/Order/{id}",
            ApiResponse<Guid>.Ok(
                id,
                "Order created successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Lấy chi tiết đơn hàng.</summary>
    /// <param name="id">ID đơn hàng (transactionId).</param>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderDetail(Guid id)
    {
        var result = await _orderService.GetOrderDetailAsync(id);
        return Ok(
            ApiResponse<OrderDetailResponse>.Ok(
                result,
                "Get order detail successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Danh sách đơn hàng theo cửa hàng (phân trang).</summary>
    /// <param name="businessId">ID cửa hàng.</param>
    /// <param name="page">Trang hiện tại (bắt đầu từ 1).</param>
    /// <param name="pageSize">Số bản ghi mỗi trang.</param>
    [HttpGet("business/{businessId}")]
    public async Task<IActionResult> GetOrders(Guid businessId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _orderService.GetOrdersByBusinessAsync(businessId, page, pageSize);
        return Ok(
            ApiResponse<PagedResult<OrderSummaryResponse>>.Ok(
                result,
                "Get orders successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Thêm sản phẩm vào đơn nháp.</summary>
    /// <param name="id">ID đơn hàng.</param>
    /// <param name="request">Thông tin sản phẩm và số lượng.</param>
    [HttpPost("{id}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddOrderItemRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _orderService.AddItemAsync(id, request);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Item added successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Cập nhật dòng hàng trong đơn nháp.</summary>
    /// <param name="id">ID đơn hàng.</param>
    /// <param name="itemId">ID dòng hàng.</param>
    /// <param name="request">Thông tin cập nhật.</param>
    [HttpPut("{id}/items/{itemId}")]
    public async Task<IActionResult> UpdateItem(Guid id, Guid itemId, [FromBody] UpdateOrderItemRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _orderService.UpdateItemAsync(id, itemId, request);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Item updated successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Xóa dòng hàng khỏi đơn nháp.</summary>
    /// <param name="id">ID đơn hàng.</param>
    /// <param name="itemId">ID dòng hàng.</param>
    [HttpDelete("{id}/items/{itemId}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId)
    {
        await _orderService.RemoveItemAsync(id, itemId);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Item removed successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Áp dụng giảm giá toàn đơn.</summary>
    /// <param name="id">ID đơn hàng.</param>
    /// <param name="request">Loại và giá trị giảm giá.</param>
    [HttpPost("{id}/discount")]
    public async Task<IActionResult> ApplyDiscount(Guid id, [FromBody] ApplyDiscountRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _orderService.ApplyDiscountAsync(id, request);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Discount applied successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Xóa giảm giá toàn đơn.</summary>
    /// <param name="id">ID đơn hàng.</param>
    [HttpDelete("{id}/discount")]
    public async Task<IActionResult> RemoveDiscount(Guid id)
    {
        await _orderService.RemoveDiscountAsync(id);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Discount removed successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Áp dụng phụ thu toàn đơn.</summary>
    /// <param name="id">ID đơn hàng.</param>
    /// <param name="request">Thông tin phụ thu.</param>
    [HttpPost("{id}/surcharge")]
    public async Task<IActionResult> ApplySurcharge(Guid id, [FromBody] ApplySurchargeRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _orderService.ApplySurchargeAsync(id, request);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Surcharge applied successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Xóa phụ thu toàn đơn.</summary>
    /// <param name="id">ID đơn hàng.</param>
    [HttpDelete("{id}/surcharge")]
    public async Task<IActionResult> RemoveSurcharge(Guid id)
    {
        await _orderService.RemoveSurchargeAsync(id);
        return Ok(
            ApiResponse<string>.Ok(
                "Success",
                "Surcharge removed successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Thanh toán và phát hành hóa đơn.</summary>
    /// <param name="id">ID đơn hàng.</param>
    /// <param name="request">Danh sách khoản thanh toán.</param>
    [HttpPost("{id}/checkout")]
    public async Task<IActionResult> Checkout(Guid id, [FromBody] CheckoutRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await _orderService.CheckoutAsync(id, request);
        return Ok(
            ApiResponse<InvoiceDetailResponse>.Ok(
                result,
                "Checkout successfully",
                HttpContext.TraceIdentifier));
    }

    /// <summary>Hủy đơn hàng nháp.</summary>
    /// <param name="id">ID đơn hàng.</param>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        await _orderService.CancelOrderAsync(id);
        return Ok();
    }
}
