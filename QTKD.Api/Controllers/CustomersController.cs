using Microsoft.AspNetCore.Mvc;
using QTKD.Api.Dtos;
using QTKD.Api.Services;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(CustomerService customerService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CustomerResponse>>> GetCustomers(
        CancellationToken cancellationToken)
    {
        return Ok(await customerService.GetAllAsync(cancellationToken));
    }
}
