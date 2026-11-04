namespace QTKD.Api.Dtos;

public sealed record SaveCustomerRequest(string? Name, string? Status = "Active");
