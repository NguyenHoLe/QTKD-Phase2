using Microsoft.EntityFrameworkCore;
using QTKD.Api.Common;
using QTKD.Api.Data;
using QTKD.Api.Dtos;
using QTKD.Api.Models;

namespace QTKD.Api.Services;

public sealed class PricingService(QtkdDbContext dbContext)
{
    public Task<List<PriceListResponse>> GetPriceListsAsync(CancellationToken cancellationToken)
    {
        return dbContext.PriceLists
            .AsNoTracking()
            .OrderBy(priceList => priceList.Id)
            .Select(priceList => new PriceListResponse(
                priceList.Id,
                priceList.Name,
                priceList.EffectiveDate,
                priceList.Items.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<PriceListResponse?> GetPriceListByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await dbContext.PriceLists
            .AsNoTracking()
            .Where(priceList => priceList.Id == id)
            .Select(priceList => new PriceListResponse(
                priceList.Id,
                priceList.Name,
                priceList.EffectiveDate,
                priceList.Items.Count))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OperationResult<PriceListResponse>> CreatePriceListAsync(
        SavePriceListRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Name is required.");
        }
        else if (request.Name.Trim().Length > 200)
        {
            errors.Add("Name must be 200 characters or fewer.");
        }

        if (request.EffectiveDate == default)
        {
            errors.Add("EffectiveDate is required.");
        }

        if (errors.Count > 0)
        {
            return OperationResult<PriceListResponse>.Failure(
                ApiErrorCodes.ValidationFailed, "PriceList data is invalid.", errors);
        }

        var priceList = new PriceList
        {
            Name = request.Name!.Trim(),
            EffectiveDate = request.EffectiveDate
        };
        dbContext.PriceLists.Add(priceList);
        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<PriceListResponse>.Success(
            new PriceListResponse(priceList.Id, priceList.Name, priceList.EffectiveDate, 0));
    }

    public async Task<OperationResult<List<PriceItemResponse>>> GetPriceItemsAsync(
        int priceListId,
        CancellationToken cancellationToken)
    {
        var listExists = await dbContext.PriceLists.AnyAsync(
            priceList => priceList.Id == priceListId,
            cancellationToken);
        if (!listExists)
        {
            return OperationResult<List<PriceItemResponse>>.Failure(
                ApiErrorCodes.NotFound, $"PriceList {priceListId} was not found.");
        }

        var items = await dbContext.PriceItems
            .AsNoTracking()
            .Where(item => item.PriceListId == priceListId)
            .OrderBy(item => item.ServiceCode)
            .Select(item => new PriceItemResponse(
                item.PriceListId,
                item.ServiceCode,
                item.UnitPrice))
            .ToListAsync(cancellationToken);

        return OperationResult<List<PriceItemResponse>>.Success(items);
    }

    public async Task<OperationResult<PriceItemResponse>> AddPriceItemAsync(
        int priceListId,
        SavePriceItemRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var serviceCode = request.ServiceCode?.Trim();
        if (string.IsNullOrWhiteSpace(serviceCode))
        {
            errors.Add("ServiceCode is required.");
        }
        else if (serviceCode.Length > 100)
        {
            errors.Add("ServiceCode must be 100 characters or fewer.");
        }

        if (request.UnitPrice <= 0)
        {
            errors.Add("UnitPrice must be greater than zero.");
        }

        var priceList = await dbContext.PriceLists
            .SingleOrDefaultAsync(item => item.Id == priceListId, cancellationToken);
        if (priceList is null)
        {
            return OperationResult<PriceItemResponse>.Failure(
                ApiErrorCodes.NotFound, $"PriceList {priceListId} was not found.");
        }

        if (errors.Count > 0)
        {
            return OperationResult<PriceItemResponse>.Failure(
                ApiErrorCodes.ValidationFailed, "PriceItem data is invalid.", errors);
        }

        var exists = await dbContext.PriceItems.AnyAsync(
            item => item.PriceListId == priceListId && item.ServiceCode == serviceCode,
            cancellationToken);
        if (exists)
        {
            return OperationResult<PriceItemResponse>.Failure(
                ApiErrorCodes.Conflict, $"ServiceCode {serviceCode} already exists in PriceList {priceListId}.");
        }

        var priceItem = new PriceItem
        {
            PriceListId = priceList.Id,
            ServiceCode = serviceCode!,
            UnitPrice = request.UnitPrice,
            PriceList = priceList
        };
        dbContext.PriceItems.Add(priceItem);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // A concurrent request added the same ServiceCode after the check above;
            // the (PriceListId, ServiceCode) primary key rejected this insert.
            return DuplicateServiceCode(serviceCode!, priceListId);
        }

        return OperationResult<PriceItemResponse>.Success(
            new PriceItemResponse(priceItem.PriceListId, priceItem.ServiceCode, priceItem.UnitPrice));
    }

    private static OperationResult<PriceItemResponse> DuplicateServiceCode(
        string serviceCode,
        int priceListId) =>
        OperationResult<PriceItemResponse>.Failure(
            ApiErrorCodes.Conflict, $"ServiceCode {serviceCode} already exists in PriceList {priceListId}.");
}
