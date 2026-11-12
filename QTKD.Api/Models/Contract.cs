namespace QTKD.Api.Models;

public class Contract
{
    public int Id { get; set; }

    public string ContractNo { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? ContentSummary { get; set; }

    public string? AttachmentFileName { get; set; }

    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Optimistic concurrency token mapped to PostgreSQL's system column <c>xmin</c>.
    /// If another request changes the row after it was read, SaveChanges fails instead of
    /// silently overwriting (e.g. an edit racing with a submit).
    /// </summary>
    public uint Version { get; set; }

    public Customer? Customer { get; set; }

    public ContractSubmission? Submission { get; set; }
}
