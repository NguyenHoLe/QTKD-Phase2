namespace QTKD.Api.Models;

public static class ContractStatuses
{
    public const string Draft = "Draft";
    public const string RevisionRequested = "RevisionRequested";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";

    public static bool CanEdit(string status) =>
        string.Equals(status, Draft, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, RevisionRequested, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Revision Requested", StringComparison.OrdinalIgnoreCase);
}
