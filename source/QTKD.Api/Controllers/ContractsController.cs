using Microsoft.AspNetCore.Mvc;
using QTKD.Api.Dtos;
using QTKD.Api.Services;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/contracts")]
public class ContractsController(ContractService contractService) : ControllerBase
{
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
}
