namespace QTKD.Api.Dtos;

/// <summary>
/// Status is intentionally omitted: a new contract always starts as Draft,
/// and status transitions are controlled by workflow endpoints.
/// </summary>
public sealed record SaveContractRequest(
    string? ContractNo,
    int CustomerId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? ContentSummary = null,
    string? AttachmentFileName = null);
