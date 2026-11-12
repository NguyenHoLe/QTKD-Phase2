using Microsoft.AspNetCore.Mvc;
using QTKD.Api.Dtos;
using QTKD.Api.Services;

namespace QTKD.Api.Controllers;

[ApiController]
[Route("api/price-lists")]
public class PricingController(PricingService pricingService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<PriceListResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PriceListResponse>>> GetPriceLists(
        CancellationToken cancellationToken)
    {
        return Ok(await pricingService.GetPriceListsAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<PriceListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PriceListResponse>> GetPriceListById(
        int id,
        CancellationToken cancellationToken)
    {
        var priceList = await pricingService.GetPriceListByIdAsync(id, cancellationToken);
        return priceList is null ? NotFoundError("PriceList", id) : Ok(priceList);
    }

    [HttpPost]
    [ProducesResponseType<PriceListResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PriceListResponse>> CreatePriceList(
        SavePriceListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pricingService.CreatePriceListAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return ToErrorResult<PriceListResponse>(result.Error!);
        }

        return CreatedAtAction(
            nameof(GetPriceListById),
            new { id = result.Value!.Id },
            result.Value);
    }

    [HttpGet("{id:int}/items")]
    [ProducesResponseType<List<PriceItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<PriceItemResponse>>> GetPriceItems(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await pricingService.GetPriceItemsAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return ToErrorResult<List<PriceItemResponse>>(result.Error!);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:int}/items")]
    [ProducesResponseType<PriceItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(QTKD.Api.Common.ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PriceItemResponse>> AddPriceItem(
        int id,
        SavePriceItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pricingService.AddPriceItemAsync(id, request, cancellationToken);
        if (!result.Succeeded)
        {
            return ToErrorResult<PriceItemResponse>(result.Error!);
        }

        return CreatedAtAction(nameof(GetPriceItems), new { id }, result.Value);
    }
}
