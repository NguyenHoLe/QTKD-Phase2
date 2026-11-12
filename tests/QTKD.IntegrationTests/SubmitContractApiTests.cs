using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Common;
using QTKD.Api.Dtos;
using QTKD.Api.Models;
using QTKD.IntegrationTests.Infrastructure;

namespace QTKD.IntegrationTests;

/// <summary>US03 — POST /api/contracts/{id}/submit (TC05, TC06, TC08 and the week 06 demo flow).</summary>
[Collection(ApiCollection.Name)]
public class SubmitContractApiTests(QtkdApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("TC", "TC05")]
    public async Task TC05_SubmitWithoutRequiredContentAndFile_IsRejected_AndStaysDraft()
    {
        var customer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: false);

        var response = await SubmitAsync(draft.Id);

        // AC03: 422 with every unmet condition
        var error = await ReadErrorAsync(
            response, HttpStatusCode.UnprocessableEntity, ApiErrorCodes.SubmissionValidationFailed);
        Assert.Equal(2, error.Details!.Count);
        Assert.Contains(error.Details, detail => detail.Contains("ContentSummary"));
        Assert.Contains(error.Details, detail => detail.Contains("AttachmentFileName"));

        // Data unchanged: still Draft, no approval hand-off created
        await using var db = CreateDbContext();
        var stored = await db.Contracts.AsNoTracking().SingleAsync(item => item.Id == draft.Id);
        Assert.Equal(ContractStatuses.Draft, stored.Status);
        Assert.False(await db.Set<ContractSubmission>().AnyAsync(item => item.ContractId == draft.Id));
    }

    [Theory]
    [Trait("TC", "TC05")]
    [InlineData(true, false, "AttachmentFileName")]
    [InlineData(false, true, "ContentSummary")]
    public async Task TC05_SubmitMissingOneRequiredItem_NamesThatItem(
        bool withContent, bool withAttachment, string expectedMissing)
    {
        var customer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: false);
        var update = await Client.PutAsJsonAsync($"/api/contracts/{draft.Id}", new
        {
            contractNo = draft.ContractNo,
            customerId = customer.Id,
            startDate = StartDate,
            endDate = EndDate,
            contentSummary = withContent ? "Có nội dung." : null,
            attachmentFileName = withAttachment ? "co-tep.pdf" : null
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var error = await ReadErrorAsync(
            await SubmitAsync(draft.Id),
            HttpStatusCode.UnprocessableEntity,
            ApiErrorCodes.SubmissionValidationFailed);

        var detail = Assert.Single(error.Details!);
        Assert.Contains(expectedMissing, detail);
        await using var db = CreateDbContext();
        Assert.Equal(ContractStatuses.Draft, (await db.Contracts.FindAsync(draft.Id))!.Status);
    }

    [Fact]
    [Trait("TC", "TC05")]
    public async Task TC05_SubmitWhenCustomerBecameInactive_IsRejected_AndStaysDraft()
    {
        var customer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: true);
        var deactivate = await Client.PutAsJsonAsync(
            $"/api/customers/{customer.Id}",
            new { name = customer.Name, status = "Inactive" });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var error = await ReadErrorAsync(
            await SubmitAsync(draft.Id),
            HttpStatusCode.UnprocessableEntity,
            ApiErrorCodes.SubmissionValidationFailed);

        Assert.Contains(error.Details!, detail => detail.Contains("Customer must exist and be Active"));
        await using var db = CreateDbContext();
        Assert.Equal(ContractStatuses.Draft, (await db.Contracts.FindAsync(draft.Id))!.Status);
        Assert.False(await db.Set<ContractSubmission>().AnyAsync(item => item.ContractId == draft.Id));
    }

    [Fact]
    [Trait("TC", "TC06")]
    public async Task TC06_SubmitValidDraft_MovesToPendingApproval_AndRecordsHandOff()
    {
        var customer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: true);

        var response = await SubmitAsync(draft.Id);

        // HTTP status + body
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadAsync<ContractSubmissionResponse>(response);
        Assert.Equal(draft.Id, result.ContractId);
        Assert.Equal(ContractStatuses.PendingApproval, result.Status);

        // Database: status changed (CTR-03: PendingApproval, never Approved) + one hand-off row
        await using var db = CreateDbContext();
        var stored = await db.Contracts.AsNoTracking()
            .Include(item => item.Submission)
            .SingleAsync(item => item.Id == draft.Id);
        Assert.Equal(ContractStatuses.PendingApproval, stored.Status);
        Assert.NotNull(stored.Submission);
        Assert.Equal(result.SubmittedAtUtc, stored.Submission.SubmittedAtUtc, TimeSpan.FromMilliseconds(1));

        // Read status back through the API
        var read = await Client.GetFromJsonAsync<ContractResponse>($"/api/contracts/{draft.Id}");
        Assert.Equal(ContractStatuses.PendingApproval, read!.Status);
        Assert.NotNull(read.SubmittedAtUtc);
    }

    [Fact]
    [Trait("TC", "TC08")]
    public async Task TC08_SubmitSameContractTwice_SecondIsRejected_AndOnlyOneHandOffExists()
    {
        var customer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: true);
        var first = await ReadAsync<ContractSubmissionResponse>(await SubmitAsync(draft.Id));

        var second = await SubmitAsync(draft.Id);

        await ReadErrorAsync(second, HttpStatusCode.Conflict, ApiErrorCodes.Conflict);
        await using var db = CreateDbContext();
        var submissions = await db.Set<ContractSubmission>()
            .Where(item => item.ContractId == draft.Id)
            .ToListAsync();
        var submission = Assert.Single(submissions);
        Assert.Equal(first.SubmittedAtUtc, submission.SubmittedAtUtc, TimeSpan.FromMilliseconds(1));
        Assert.Equal(ContractStatuses.PendingApproval, (await db.Contracts.FindAsync(draft.Id))!.Status);
    }

    [Fact]
    [Trait("TC", "TC08")]
    public async Task TC08_ConcurrentSubmits_CreateExactlyOneHandOff()
    {
        var customer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: true);

        // AC05: ten "Gửi" clicks arriving at the same time
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => SubmitAsync(draft.Id)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.OK),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        await using var db = CreateDbContext();
        Assert.Equal(1, await db.Set<ContractSubmission>().CountAsync(item => item.ContractId == draft.Id));
    }

    [Fact]
    public async Task SubmitUnknownContract_Returns404()
    {
        await ReadErrorAsync(await SubmitAsync(2147000000), HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
    }

    /// <summary>
    /// The week 06 demo flow as one automated scenario:
    /// create Customer → create Draft → submit incomplete (fails) → complete → submit (succeeds)
    /// → check the database → submit again (rejected).
    /// </summary>
    [Fact]
    [Trait("TC", "Demo")]
    public async Task DemoFlow_CustomerToContractToSubmit()
    {
        var customer = await CreateCustomerAsync();
        var draft = await CreateDraftContractAsync(customer.Id, complete: false);

        var failed = await SubmitAsync(draft.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode);

        var completed = await Client.PutAsJsonAsync(
            $"/api/contracts/{draft.Id}",
            ContractBody(draft.ContractNo, customer.Id, complete: true));
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        var succeeded = await SubmitAsync(draft.Id);
        Assert.Equal(HttpStatusCode.OK, succeeded.StatusCode);

        await using (var db = CreateDbContext())
        {
            var stored = await db.Contracts.AsNoTracking()
                .Include(item => item.Submission)
                .SingleAsync(item => item.Id == draft.Id);
            Assert.Equal(ContractStatuses.PendingApproval, stored.Status);
            Assert.NotNull(stored.Submission);
        }

        var repeated = await SubmitAsync(draft.Id);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
    }
}
