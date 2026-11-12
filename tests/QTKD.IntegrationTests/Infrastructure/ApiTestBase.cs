using System.Net;
using System.Net.Http.Json;
using QTKD.Api.Common;
using QTKD.Api.Data;
using QTKD.Api.Dtos;

namespace QTKD.IntegrationTests.Infrastructure;

/// <summary>Shared HTTP helpers and test-data builders for the integration tests.</summary>
public abstract class ApiTestBase(QtkdApiFactory factory)
{
    protected static readonly DateOnly StartDate = new(2026, 11, 1);
    protected static readonly DateOnly EndDate = new(2027, 10, 31);

    protected QtkdApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    protected QtkdDbContext CreateDbContext() => Factory.CreateDbContext();

    /// <summary>Unique suffix so every test creates its own customers/contracts.</summary>
    protected static string NewSuffix() => Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    protected async Task<CustomerResponse> CreateCustomerAsync(string status = "Active")
    {
        var response = await Client.PostAsJsonAsync(
            "/api/customers",
            new { name = $"Integration Customer {NewSuffix()}", status });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<CustomerResponse>(response);
    }

    /// <summary>Request body for create/update. Pass complete:false to leave out the CTR-02 content/file.</summary>
    protected static object ContractBody(string contractNo, int customerId, bool complete) => new
    {
        contractNo,
        customerId,
        startDate = StartDate,
        endDate = EndDate,
        contentSummary = complete ? "Cung cấp dịch vụ cảng theo phạm vi đã thống nhất." : null,
        attachmentFileName = complete ? $"{contractNo}.pdf" : null
    };

    protected async Task<ContractResponse> CreateDraftContractAsync(int customerId, bool complete)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/contracts",
            ContractBody($"HD-IT-{NewSuffix()}", customerId, complete));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<ContractResponse>(response);
    }

    protected Task<HttpResponseMessage> SubmitAsync(int contractId) =>
        Client.PostAsync($"/api/contracts/{contractId}/submit", content: null);

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);
        return value;
    }

    /// <summary>Asserts the shared error shape {code, message, details} and returns it.</summary>
    protected static async Task<ApiErrorResponse> ReadErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var error = await ReadAsync<ApiErrorResponse>(response);
        Assert.Equal(expectedCode, error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        return error;
    }
}
