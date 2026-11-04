namespace QTKD.Api.Dtos;

public sealed record PriceListResponse(int Id, string Name, DateOnly EffectiveDate, int ItemCount);
