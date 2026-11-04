namespace QTKD.Api.Models;

/// <summary>
/// Records one simulated hand-off to the approval workflow.
/// This does not implement approval steps or reviewer assignment.
/// </summary>
public class ContractSubmission
{
    public int ContractId { get; set; }

    public DateTime SubmittedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}
