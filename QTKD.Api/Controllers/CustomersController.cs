using Microsoft.AspNetCore.Mvc;
using QTKD.Api.Dtos;
using QTKD.Api.Services;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(CustomerService customerService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CustomerResponse>>> GetCustomers(
        CancellationToken cancellationToken)
    {
        return Ok(await customerService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetCustomerById(
        int id,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.GetByIdAsync(id, cancellationToken);
        return customer is null ? NotFoundError("Customer", id) : Ok(customer);
    }

    [HttpPost]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerResponse>> CreateCustomer(
        SaveCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await customerService.CreateAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return ToErrorResult<CustomerResponse>(result.Error!);
        }

        return CreatedAtAction(
            nameof(GetCustomerById),
            new { id = result.Value!.Id },
            result.Value);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> UpdateCustomer(
        int id,
        SaveCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await customerService.UpdateAsync(id, request, cancellationToken);
        if (!result.Succeeded)
        {
            return ToErrorResult<CustomerResponse>(result.Error!);
        }

        return Ok(result.Value);
    }
}
