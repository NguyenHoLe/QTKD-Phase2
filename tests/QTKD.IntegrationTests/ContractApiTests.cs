using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Common;
using QTKD.Api.Dtos;
using QTKD.Api.Models;
using QTKD.IntegrationTests.Infrastructure;

namespace QTKD.IntegrationTests;

/// <summary>Create/read/update Contract, CTR-01 (TC03, TC04) and CTR-03 through the API (TC07).</summary>
[Collection(ApiCollection.Name)]
public class ContractApiTests(QtkdApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task GetSeedContract_ReturnsDraftForMinhPhat()
    {
        var contract = await Client.GetFromJsonAsync<ContractResponse>("/api/contracts/1");

        Assert.Equal("HD-2026-018", contract!.ContractNo);
        Assert.Equal("Công ty Minh Phát", contract.CustomerName);
        Assert.Equal(ContractStatuses.Draft, contract.Status);
    }

    [Fact]
    public async Task CreateContract_StartsInDraft_AndIsStored()
    {
        var customer = await CreateCustomerAsync();
        var contractNo = $"HD-IT-{NewSuffix()}";

        var response = await Client.PostAsJsonAsync("/api/contracts", ContractBody(contractNo, customer.Id, complete: false));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<ContractResponse>(response);
        Assert.Equal(ContractStatuses.Draft, created.Status);
        Assert.Equal(customer.Name, created.CustomerName);
        Assert.Null(created.SubmittedAtUtc);

        await using var db = CreateDbContext();
        var stored = await db.Contracts.AsNoTracking().SingleAsync(item => item.Id == created.Id);
        Assert.Equal(contractNo, stored.ContractNo);
        Assert.Equal(customer.Id, stored.CustomerId);
        Assert.Equal(StartDate, stored.StartDate);
        Assert.Equal(EndDate, stored.EndDate);
        Assert.Equal(ContractStatuses.Draft, stored.Status);
    }

    [Fact]
    public async Task CreateContract_ForInactiveCustomer_IsRejected()
    {
        var inactive = await CreateCustomerAsync("Inactive");

        var response = await Client.PostAsJsonAsync(
            "/api/contracts",
            ContractBody($"HD-IT-{NewSuffix()}", inactive.Id, complete: true));

        var error = await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        Assert.Contains("CustomerId must reference an existing Active customer.", error.Details!);
    }

    [Fact]
    public async Task CreateContract_WithEndDateBeforeStartDate_IsRejected()
    {
        var customer = await CreateCustomerAsync();

        var response = await Client.PostAsJsonAsync("/api/contracts", new
        {
            contractNo = $"HD-IT-{NewSuffix()}",
            customerId = customer.Id,
            startDate = EndDate,
            endDate = StartDate
        });

        var error = await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        Assert.Contains("EndDate must be on or after StartDate.", error.Details!);
    }

    [Fact]
    public async Task CreateContract_WithDuplicateContractNo_IsRejected()
    {
        var customer = await CreateCustomerAsync();
        var existing = await CreateDraftContractAsync(customer.Id, complete: false);

        var response = await Client.PostAsJsonAsync(
            "/api/contracts",
            ContractBody(existing.ContractNo, customer.Id, complete: false));

        var error = await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        Assert.Contains("ContractNo must be unique.", error.Details!);
    }

    [Fact]
    [Trait("TC", "TC03")]
    public async Task TC03_UpdateDraftContract_WithValidData_Succeeds_AndIsStored()
    {
        var customer = await CreateCustomerAsync();
        var otherCustomer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: false);
        var newContractNo = $"HD-IT-{NewSuffix()}";

        var response = await Client.PutAsJsonAsync($"/api/contracts/{draft.Id}", new
        {
            contractNo = newContractNo,
            customerId = otherCustomer.Id,
            startDate = new DateOnly(2026, 12, 1),
            endDate = new DateOnly(2027, 11, 30),
            contentSummary = "Nội dung đã bổ sung.",
            attachmentFileName = "phu-luc.pdf"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<ContractResponse>(response);
        Assert.Equal(newContractNo, updated.ContractNo);
        Assert.Equal(ContractStatuses.Draft, updated.Status);

        await using var db = CreateDbContext();
        var stored = await db.Contracts.AsNoTracking().SingleAsync(item => item.Id == draft.Id);
        Assert.Equal(newContractNo, stored.ContractNo);
        Assert.Equal(otherCustomer.Id, stored.CustomerId);
        Assert.Equal(new DateOnly(2026, 12, 1), stored.StartDate);
        Assert.Equal(new DateOnly(2027, 11, 30), stored.EndDate);
        Assert.Equal("Nội dung đã bổ sung.", stored.ContentSummary);
        Assert.Equal("phu-luc.pdf", stored.AttachmentFileName);
        Assert.Equal(ContractStatuses.Draft, stored.Status);
    }

    [Fact]
    [Trait("TC", "TC03")]
    public async Task TC03_UpdateDraftContract_WithInvalidData_IsRejected_AndDataStays()
    {
        var customer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: true);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{draft.Id}", new
        {
            contractNo = draft.ContractNo,
            customerId = customer.Id,
            startDate = EndDate,
            endDate = StartDate
        });

        await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        await using var db = CreateDbContext();
        var stored = await db.Contracts.AsNoTracking().SingleAsync(item => item.Id == draft.Id);
        Assert.Equal(StartDate, stored.StartDate);
        Assert.Equal(EndDate, stored.EndDate);
        Assert.Equal(draft.ContentSummary, stored.ContentSummary);
    }

    [Fact]
    [Trait("TC", "TC04")]
    public async Task TC04_UpdatePendingApprovalContract_IsRejected_AndDataStays()
    {
        var customer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: true);
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(draft.Id)).StatusCode);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{draft.Id}", new
        {
            contractNo = $"HD-IT-{NewSuffix()}",
            customerId = customer.Id,
            startDate = StartDate,
            endDate = EndDate,
            contentSummary = "Sửa sau khi đã gửi trình duyệt.",
            attachmentFileName = "khac.pdf"
        });

        var error = await ReadErrorAsync(response, HttpStatusCode.Conflict, ApiErrorCodes.Conflict);
        Assert.Contains("CTR-01", error.Message);

        await using var db = CreateDbContext();
        var stored = await db.Contracts.AsNoTracking().SingleAsync(item => item.Id == draft.Id);
        Assert.Equal(draft.ContractNo, stored.ContractNo);
        Assert.Equal(draft.ContentSummary, stored.ContentSummary);
        Assert.Equal(draft.AttachmentFileName, stored.AttachmentFileName);
        Assert.Equal(ContractStatuses.PendingApproval, stored.Status);
    }

    [Fact]
    [Trait("TC", "TC07")]
    public async Task TC07_CreateContractWithApprovedStatus_StillStartsInDraft()
    {
        var customer = await CreateCustomerAsync();

        var response = await Client.PostAsJsonAsync("/api/contracts", new
        {
            contractNo = $"HD-IT-{NewSuffix()}",
            customerId = customer.Id,
            startDate = StartDate,
            endDate = EndDate,
            status = "Approved"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<ContractResponse>(response);
        await using var db = CreateDbContext();
        Assert.Equal(ContractStatuses.Draft, (await db.Contracts.FindAsync(created.Id))!.Status);
    }

    [Fact]
    [Trait("TC", "TC07")]
    public async Task TC07_UpdateDraftContractToApproved_DoesNotChangeStatus()
    {
        var customer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: true);

        var response = await Client.PutAsJsonAsync($"/api/contracts/{draft.Id}", new
        {
            contractNo = draft.ContractNo,
            customerId = customer.Id,
            startDate = StartDate,
            endDate = EndDate,
            contentSummary = draft.ContentSummary,
            attachmentFileName = draft.AttachmentFileName,
            status = "Approved"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = CreateDbContext();
        var stored = await db.Contracts.AsNoTracking().SingleAsync(item => item.Id == draft.Id);
        Assert.Equal(ContractStatuses.Draft, stored.Status);
        Assert.NotEqual(ContractStatuses.Approved, stored.Status);
    }

    [Fact]
    public async Task UpdateUnknownContract_Returns404()
    {
        var response = await Client.PutAsJsonAsync(
            "/api/contracts/2147000000",
            ContractBody($"HD-IT-{NewSuffix()}", 1, complete: true));

        await ReadErrorAsync(response, HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
    }

    [Fact]
    [Trait("Bug", "BUG-02")]
    public async Task CreateContractWithInvalidDate_ReturnsSharedErrorShape()
    {
        var response = await Client.PostAsync(
            "/api/contracts",
            new StringContent(
                """{"contractNo":"X","customerId":1,"startDate":"abc","endDate":"2027-01-01"}""",
                System.Text.Encoding.UTF8,
                "application/json"));

        var error = await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.InvalidRequest);
        Assert.Equal(["startDate: value is missing or has an invalid format."], error.Details);
    }

    [Fact]
    [Trait("Bug", "BUG-02")]
    public async Task GetUnknownContract_Returns404WithErrorBody()
    {
        var response = await Client.GetAsync("/api/contracts/2147000000");

        var error = await ReadErrorAsync(response, HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
        Assert.Equal("Contract 2147000000 was not found.", error.Message);
    }
}
