namespace QTKD.Api.Dtos;

public sealed record ContractResponse(
    int Id,
    string ContractNo,
    int CustomerId,
    string CustomerName,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status);
