namespace QTKD.Api.Dtos;

public sealed record ContractSubmissionResponse(
    int ContractId,
    string Status,
    DateTime SubmittedAtUtc,
    string WorkflowMessage);
