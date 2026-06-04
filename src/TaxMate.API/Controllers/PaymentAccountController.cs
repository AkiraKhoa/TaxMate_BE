using Microsoft.AspNetCore.Mvc;
using TaxMate.Model.DTO;
using TaxMate.Service.Interfaces;

namespace TaxMate.API.Controllers;

[Controller]
[Route("api/[controller]")]
public class PaymentAccountController : ControllerBase
{
    private readonly IPaymentAccountService _paymentAccountService;

    public PaymentAccountController(IPaymentAccountService paymentAccountService)
    {
        _paymentAccountService = paymentAccountService;
    }

    [HttpPost("business/{businessId}")]
    public async Task<IActionResult> Create(Guid businessId, [FromBody] CreatePaymentAccountRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var id = await _paymentAccountService.CreateAsync(businessId, request);
        return Ok(id);
    }

    [HttpGet("business/{businessId}")]
    public async Task<IActionResult> GetByBusiness(Guid businessId)
    {
        var result = await _paymentAccountService.GetByBusinessIdAsync(businessId);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _paymentAccountService.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePaymentAccountRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await _paymentAccountService.UpdateAsync(id, request);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _paymentAccountService.DeleteAsync(id);
        return Ok();
    }

    [HttpPatch("{id}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id, [FromQuery] Guid businessId)
    {
        await _paymentAccountService.SetDefaultAsync(businessId, id);
        return Ok();
    }
}
