using QTKD.Api.Domain;
using QTKD.Api.Models;

namespace QTKD.UnitTests;

/// <summary>
/// CTR-03: a contract may not go from Draft straight to Approved.
/// The transition table follows the Phase 1 state machine.
/// </summary>
[Trait("Rule", "CTR-03")]
public class Ctr03TransitionRuleTests
{
    private static readonly string[] AllStatuses =
    [
        ContractStatuses.Draft,
        ContractStatuses.PendingApproval,
        ContractStatuses.RevisionRequested,
        ContractStatuses.Approved,
        ContractStatuses.Rejected,
        ContractStatuses.Effective
    ];

    [Theory]
    [InlineData(ContractStatuses.Draft, ContractStatuses.PendingApproval)] // US03 submit (TC06)
    [InlineData(ContractStatuses.PendingApproval, ContractStatuses.Approved)]
    [InlineData(ContractStatuses.PendingApproval, ContractStatuses.RevisionRequested)]
    [InlineData(ContractStatuses.PendingApproval, ContractStatuses.Rejected)]
    [InlineData(ContractStatuses.RevisionRequested, ContractStatuses.Draft)]
    [InlineData(ContractStatuses.Approved, ContractStatuses.Effective)]
    [InlineData("draft", "pendingapproval")]
    public void CanTransition_ReturnsTrue_ForTransitionsInTheStateMachine(string from, string to)
    {
        Assert.True(ContractRules.CanTransition(from, to));
    }

    [Trait("TC", "TC07")]
    [Theory]
    [InlineData(ContractStatuses.Draft, ContractStatuses.Approved)] // TC07: forbidden by CTR-03
    [InlineData(ContractStatuses.Draft, ContractStatuses.Effective)]
    [InlineData(ContractStatuses.Draft, ContractStatuses.Rejected)]
    [InlineData(ContractStatuses.Draft, ContractStatuses.Draft)]
    [InlineData(ContractStatuses.PendingApproval, ContractStatuses.PendingApproval)] // TC08: no second submit
    [InlineData(ContractStatuses.PendingApproval, ContractStatuses.Draft)]
    [InlineData(ContractStatuses.Rejected, ContractStatuses.Approved)]
    [InlineData(ContractStatuses.Effective, ContractStatuses.Draft)]
    [InlineData("UnknownStatus", ContractStatuses.PendingApproval)]
    [InlineData(null, ContractStatuses.PendingApproval)]
    [InlineData(ContractStatuses.Draft, null)]
    public void CanTransition_ReturnsFalse_ForTransitionsOutsideTheStateMachine(string? from, string? to)
    {
        Assert.False(ContractRules.CanTransition(from, to));
    }

    [Trait("TC", "TC07")]
    [Fact]
    public void OnlyPendingApproval_CanMoveToApproved()
    {
        // Stronger form of CTR-03: Approved is reachable only after the approval step.
        var sources = AllStatuses.Where(status => ContractRules.CanTransition(status, ContractStatuses.Approved));

        Assert.Equal([ContractStatuses.PendingApproval], sources);
    }

    [Trait("TC", "TC06")]
    [Fact]
    public void Submit_FromDraft_LeadsOnlyToPendingApproval()
    {
        var targets = AllStatuses.Where(status => ContractRules.CanTransition(ContractStatuses.Draft, status));

        Assert.Equal([ContractStatuses.PendingApproval], targets);
    }
}
