using QTKD.Api.Domain;
using QTKD.Api.Models;

namespace QTKD.UnitTests;

/// <summary>CTR-01: only Draft or RevisionRequested contracts may be edited.</summary>
[Trait("Rule", "CTR-01")]
public class Ctr01EditRuleTests
{
    [Trait("TC", "TC03")]
    [Theory]
    [InlineData(ContractStatuses.Draft)]
    [InlineData(ContractStatuses.RevisionRequested)]
    [InlineData("draft")] // status comparison is case-insensitive
    public void CanEdit_ReturnsTrue_ForDraftAndRevisionRequested(string status)
    {
        Assert.True(ContractRules.CanEdit(status));
    }

    [Trait("TC", "TC04")]
    [Theory]
    [InlineData(ContractStatuses.PendingApproval)] // TC04 at rule level
    [InlineData(ContractStatuses.Approved)]
    [InlineData(ContractStatuses.Rejected)]
    [InlineData(ContractStatuses.Effective)]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("UnknownStatus")]
    public void CanEdit_ReturnsFalse_ForAnyOtherStatus(string? status)
    {
        Assert.False(ContractRules.CanEdit(status));
    }
}
