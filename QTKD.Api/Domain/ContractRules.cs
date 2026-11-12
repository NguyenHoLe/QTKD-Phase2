using QTKD.Api.Models;

namespace QTKD.Api.Domain;

/// <summary>
/// Business rules for the Contract lifecycle (CTR-01, CTR-02, CTR-03).
/// The methods are pure (no database access) so they can be unit tested directly;
/// ContractService calls them before saving anything.
/// </summary>
public static class ContractRules
{
    /// <summary>
    /// Allowed status transitions taken from the Phase 1 state machine.
    /// Any pair not listed here is rejected — in particular Draft → Approved (CTR-03).
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedTransitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ContractStatuses.Draft] = [ContractStatuses.PendingApproval],
            [ContractStatuses.PendingApproval] =
            [
                ContractStatuses.RevisionRequested,
                ContractStatuses.Approved,
                ContractStatuses.Rejected
            ],
            [ContractStatuses.RevisionRequested] = [ContractStatuses.Draft],
            [ContractStatuses.Approved] = [ContractStatuses.Effective]
        };

    /// <summary>CTR-01: a contract may only be edited in Draft or RevisionRequested.</summary>
    public static bool CanEdit(string? status) =>
        string.Equals(status, ContractStatuses.Draft, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, ContractStatuses.RevisionRequested, StringComparison.OrdinalIgnoreCase);

    /// <summary>CTR-03: only transitions from the state machine are allowed.</summary>
    public static bool CanTransition(string? from, string? to) =>
        from is not null &&
        to is not null &&
        AllowedTransitions.TryGetValue(from, out var targets) &&
        targets.Contains(to, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// CTR-02: conditions that must hold before a contract can be submitted.
    /// Returns an empty list when the contract can be submitted.
    /// Expects <see cref="Contract.Customer"/> to be loaded.
    /// ContentSummary/AttachmentFileName are demo assumptions because Phase 1
    /// has not confirmed the exact list of required content/files.
    /// </summary>
    public static IReadOnlyList<string> GetSubmissionErrors(Contract contract)
    {
        var errors = new List<string>();
        if (contract.Customer is null ||
            !string.Equals(contract.Customer.Status, CustomerStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("CTR-02: Customer must exist and be Active.");
        }

        if (contract.StartDate == default || contract.EndDate == default ||
            contract.EndDate < contract.StartDate)
        {
            errors.Add("CTR-02: StartDate and EndDate must form a valid effective period.");
        }

        if (string.IsNullOrWhiteSpace(contract.ContentSummary))
        {
            errors.Add("CTR-02 demo assumption: ContentSummary is required.");
        }

        if (string.IsNullOrWhiteSpace(contract.AttachmentFileName))
        {
            errors.Add("CTR-02 demo assumption: at least one AttachmentFileName is required.");
        }

        return errors;
    }
}
