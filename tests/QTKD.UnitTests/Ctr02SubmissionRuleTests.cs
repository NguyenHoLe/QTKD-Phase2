using QTKD.Api.Domain;
using QTKD.Api.Models;

namespace QTKD.UnitTests;

/// <summary>
/// CTR-02: a contract may be submitted only when the customer is valid, the effective
/// period is valid and the required content/file are present (demo assumption).
/// </summary>
[Trait("Rule", "CTR-02")]
public class Ctr02SubmissionRuleTests
{
    private static Contract ValidContract() => new()
    {
        Id = 1,
        ContractNo = "HD-2026-018",
        CustomerId = 1,
        Customer = new Customer { Id = 1, Name = "Công ty Minh Phát", Status = CustomerStatuses.Active },
        StartDate = new DateOnly(2026, 10, 1),
        EndDate = new DateOnly(2027, 9, 30),
        ContentSummary = "Cung cấp dịch vụ cảng.",
        AttachmentFileName = "HD-2026-018.pdf",
        Status = ContractStatuses.Draft
    };

    [Trait("TC", "TC06")]
    [Fact]
    public void GetSubmissionErrors_ReturnsNoErrors_WhenAllConditionsAreMet()
    {
        Assert.Empty(ContractRules.GetSubmissionErrors(ValidContract()));
    }

    [Trait("TC", "TC06")]
    [Fact]
    public void GetSubmissionErrors_AllowsOneDayPeriod_WhenStartEqualsEnd()
    {
        var contract = ValidContract();
        contract.EndDate = contract.StartDate;

        Assert.Empty(ContractRules.GetSubmissionErrors(contract));
    }

    [Trait("TC", "TC05")]
    [Fact]
    public void GetSubmissionErrors_RejectsInactiveCustomer()
    {
        var contract = ValidContract();
        contract.Customer!.Status = CustomerStatuses.Inactive;

        var error = Assert.Single(ContractRules.GetSubmissionErrors(contract));
        Assert.Contains("Customer must exist and be Active", error);
    }

    [Trait("TC", "TC05")]
    [Fact]
    public void GetSubmissionErrors_RejectsMissingCustomer()
    {
        var contract = ValidContract();
        contract.Customer = null;

        var error = Assert.Single(ContractRules.GetSubmissionErrors(contract));
        Assert.Contains("Customer must exist and be Active", error);
    }

    [Trait("TC", "TC05")]
    [Fact]
    public void GetSubmissionErrors_RejectsEndDateBeforeStartDate()
    {
        var contract = ValidContract();
        contract.EndDate = contract.StartDate.AddDays(-1);

        var error = Assert.Single(ContractRules.GetSubmissionErrors(contract));
        Assert.Contains("valid effective period", error);
    }

    [Trait("TC", "TC05")]
    [Fact]
    public void GetSubmissionErrors_RejectsMissingDates()
    {
        var contract = ValidContract();
        contract.StartDate = default;

        var error = Assert.Single(ContractRules.GetSubmissionErrors(contract));
        Assert.Contains("valid effective period", error);
    }

    [Trait("TC", "TC05")]
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetSubmissionErrors_RejectsMissingContent(string? content)
    {
        var contract = ValidContract();
        contract.ContentSummary = content;

        var error = Assert.Single(ContractRules.GetSubmissionErrors(contract));
        Assert.Contains("ContentSummary", error);
    }

    [Trait("TC", "TC05")]
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetSubmissionErrors_RejectsMissingAttachment(string? fileName)
    {
        var contract = ValidContract();
        contract.AttachmentFileName = fileName;

        var error = Assert.Single(ContractRules.GetSubmissionErrors(contract));
        Assert.Contains("AttachmentFileName", error);
    }

    [Trait("TC", "TC05")]
    [Fact]
    public void GetSubmissionErrors_ListsEveryUnmetCondition()
    {
        // AC03: the user must see all unmet conditions at once, not only the first one.
        var contract = ValidContract();
        contract.Customer!.Status = CustomerStatuses.Inactive;
        contract.EndDate = contract.StartDate.AddDays(-1);
        contract.ContentSummary = null;
        contract.AttachmentFileName = null;

        Assert.Equal(4, ContractRules.GetSubmissionErrors(contract).Count);
    }
}
