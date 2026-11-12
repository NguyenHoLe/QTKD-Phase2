using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Common;
using QTKD.Api.Models;
using QTKD.IntegrationTests.Infrastructure;

namespace QTKD.IntegrationTests;

/// <summary>
/// Regression tests for the concurrency bugs recorded in docs/bug-log.md.
/// Requests are sent in parallel to the in-memory API, which uses the real PostgreSQL database.
/// </summary>
[Collection(ApiCollection.Name)]
public class ConcurrencyRegressionTests(QtkdApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("Bug", "BUG-01")]
    public async Task ConcurrentCreatesWithSameContractNo_NeverReturn500_AndStoreOneContract()
    {
        var customer = await CreateCustomerAsync();
        var contractNo = $"HD-RACE-{NewSuffix()}";

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Client.PostAsJsonAsync("/api/contracts", ContractBody(contractNo, customer.Id, complete: false))));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        foreach (var rejected in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
        {
            var error = await ReadErrorAsync(rejected, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
            Assert.Contains("ContractNo must be unique.", error.Details!);
        }

        await using var db = CreateDbContext();
        Assert.Equal(1, await db.Contracts.CountAsync(contract => contract.ContractNo == contractNo));
    }

    [Fact]
    [Trait("Bug", "BUG-01")]
    public async Task ConcurrentAddsOfSameServiceCode_NeverReturn500_AndStoreOneItem()
    {
        var serviceCode = $"SVC-RACE-{NewSuffix()}";

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            Client.PostAsJsonAsync("/api/price-lists/1/items", new { serviceCode, unitPrice = 1000m })));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        foreach (var rejected in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
        {
            await ReadErrorAsync(rejected, HttpStatusCode.Conflict, ApiErrorCodes.Conflict);
        }

        await using var db = CreateDbContext();
        Assert.Equal(1, await db.PriceItems.CountAsync(item => item.ServiceCode == serviceCode));
    }

    [Fact]
    [Trait("Bug", "BUG-03")]
    public async Task ConcurrentUpdateAndSubmit_NeverBreakCtr01OrCtr02()
    {
        var customer = await CreateCustomerAsync();

        for (var round = 0; round < 15; round++)
        {
            var draft = await CreateDraftContractAsync(customer.Id, complete: true);

            // One request removes the required content while another submits the contract.
            var clearContent = Client.PutAsJsonAsync(
                $"/api/contracts/{draft.Id}",
                ContractBody(draft.ContractNo, customer.Id, complete: false));
            var submit = SubmitAsync(draft.Id);
            await Task.WhenAll(clearContent, submit);
            var editStatus = (await clearContent).StatusCode;
            var submitStatus = (await submit).StatusCode;

            Assert.Contains(editStatus, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            Assert.Contains(
                submitStatus,
                new[] { HttpStatusCode.OK, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity });
            // Both cannot win: either the edit or the submission must be rejected.
            Assert.False(
                editStatus == HttpStatusCode.OK && submitStatus == HttpStatusCode.OK,
                $"Round {round}: both the edit and the submit succeeded.");

            await using var db = CreateDbContext();
            var stored = await db.Contracts.AsNoTracking()
                .Include(item => item.Submission)
                .SingleAsync(item => item.Id == draft.Id);
            if (stored.Status == ContractStatuses.PendingApproval)
            {
                // CTR-02 still holds for what was submitted, and CTR-01: nothing was edited afterwards.
                Assert.False(string.IsNullOrWhiteSpace(stored.ContentSummary));
                Assert.False(string.IsNullOrWhiteSpace(stored.AttachmentFileName));
                Assert.NotNull(stored.Submission);
            }
            else
            {
                Assert.Equal(ContractStatuses.Draft, stored.Status);
                Assert.Null(stored.Submission);
            }
        }
    }
}
