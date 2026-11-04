namespace QTKD.Api.Dtos;

public sealed record PriceItemResponse(int PriceListId, string ServiceCode, decimal UnitPrice);
