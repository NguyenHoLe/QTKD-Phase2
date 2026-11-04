using Microsoft.AspNetCore.Mvc;
using QTKD.Api.Dtos;
using QTKD.Api.Services;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/contracts")]
public class ContractsController(ContractService contractService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<ContractResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ContractResponse>>> GetContracts(
        CancellationToken cancellationToken)
    {
        return Ok(await contractService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContractResponse>> GetContractById(
        int id,
        CancellationToken cancellationToken)
    {
        var contract = await contractService.GetByIdAsync(id, cancellationToken);

        if (contract is null)
        {
            return NotFound();
        }

        return Ok(contract);
    }

    [HttpPost]
    [ProducesResponseType<ContractResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContractResponse>> CreateContract(
        SaveContractRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contractService.CreateAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return ToErrorResult<ContractResponse>(result.Error!);
        }

        return CreatedAtAction(
            nameof(GetContractById),
            new { id = result.Value!.Id },
            result.Value);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ContractResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContractResponse>> UpdateContract(
        int id,
        SaveContractRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contractService.UpdateAsync(id, request, cancellationToken);
        if (!result.Succeeded)
        {
            return ToErrorResult<ContractResponse>(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:int}/submit")]
    [ProducesResponseType<ContractSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ContractSubmissionResponse>> SubmitContract(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await contractService.SubmitAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return ToErrorResult<ContractSubmissionResponse>(result.Error!);
        }

        return Ok(result.Value);
    }
}
