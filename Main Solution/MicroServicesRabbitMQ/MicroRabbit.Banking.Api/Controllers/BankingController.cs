using MicroRabbit.Banking.Application.Interfaces;
using MicroRabbit.Banking.Application.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace MicroRabbit.Banking.Api.Controllers;
[ApiController, Route("api/[controller]")]
public class BankingController : ControllerBase
{
    private readonly IAccountService _service;
    public BankingController(IAccountService service) => _service = service;
    [HttpGet] public IActionResult Get() => Ok(_service.GetAccounts());
    [HttpPost] public async Task<IActionResult> Post(AccountTransfer transfer)
    {
        try { return Accepted(new { message = await _service.Transfer(transfer) }); }
        catch (ArgumentException ex) { return BadRequest(new ProblemDetails { Title = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new ProblemDetails { Title = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new ProblemDetails { Title = ex.Message }); }
    }
}
