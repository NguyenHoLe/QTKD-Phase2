namespace QTKD.Api.Models;

/// <summary>
/// Contract statuses from the Phase 1 state machine (Tuần 02, slide 8).
/// Week 06 only exposes the Draft → PendingApproval transition (US03);
/// the other statuses belong to later approval user stories.
/// </summary>
public static class ContractStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string RevisionRequested = "RevisionRequested";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Effective = "Effective";
}
