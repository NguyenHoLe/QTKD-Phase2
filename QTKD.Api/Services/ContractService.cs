using Microsoft.EntityFrameworkCore;
using QTKD.Api.Common;
using QTKD.Api.Data;
using QTKD.Api.Dtos;
using QTKD.Api.Models;

namespace QTKD.Api.Services;

public sealed class ContractService(QtkdDbContext dbContext)
{
    public async Task<List<ContractResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var contracts = await dbContext.Contracts
            .AsNoTracking()
            .Include(contract => contract.Customer)
            .Include(contract => contract.Submission)
            .OrderBy(contract => contract.Id)
            .ToListAsync(cancellationToken);

        return contracts.Select(ToResponse).ToList();
    }

    public async Task<ContractResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Submission)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return contract is null ? null : ToResponse(contract);
    }

    public async Task<OperationResult<ContractResponse>> CreateAsync(
        SaveContractRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateEditableFieldsAsync(request, null, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var customer = await dbContext.Customers
            .SingleAsync(item => item.Id == request.CustomerId, cancellationToken);
        var contract = new Contract
        {
            ContractNo = request.ContractNo!.Trim(),
            CustomerId = request.CustomerId,
            Customer = customer,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ContentSummary = NormalizeOptional(request.ContentSummary),
            AttachmentFileName = NormalizeOptional(request.AttachmentFileName),
            Status = ContractStatuses.Draft
        };

        dbContext.Contracts.Add(contract);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<ContractResponse>.Success(ToResponse(contract));
    }

    public async Task<OperationResult<ContractResponse>> UpdateAsync(
        int id,
        SaveContractRequest request,
        CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts
            .Include(item => item.Customer)
            .Include(item => item.Submission)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contract is null)
        {
            return OperationResult<ContractResponse>.Failure(
                "not_found", $"Contract {id} was not found.");
        }

        if (!ContractStatuses.CanEdit(contract.Status))
        {
            return OperationResult<ContractResponse>.Failure(
                "conflict",
                $"Contract {id} cannot be edited while its status is {contract.Status}. CTR-01 allows edits only in Draft or RevisionRequested.");
        }

        var validation = await ValidateEditableFieldsAsync(request, id, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        contract.ContractNo = request.ContractNo!.Trim();
        contract.CustomerId = request.CustomerId;
        contract.Customer = await dbContext.Customers
            .SingleAsync(item => item.Id == request.CustomerId, cancellationToken);
        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.ContentSummary = NormalizeOptional(request.ContentSummary);
        contract.AttachmentFileName = NormalizeOptional(request.AttachmentFileName);
        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<ContractResponse>.Success(ToResponse(contract));
    }

    public async Task<OperationResult<ContractSubmissionResponse>> SubmitAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var contract = await dbContext.Contracts
            .Include(item => item.Customer)
            .Include(item => item.Submission)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contract is null)
        {
            return OperationResult<ContractSubmissionResponse>.Failure(
                "not_found", $"Contract {id} was not found.");
        }

        if (!string.Equals(contract.Status, ContractStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<ContractSubmissionResponse>.Failure(
                "conflict",
                $"Contract {id} cannot be submitted from status {contract.Status}. Only Draft contracts can be submitted.");
        }

        if (contract.Submission is not null)
        {
            return OperationResult<ContractSubmissionResponse>.Failure(
                "conflict", $"Contract {id} already has a submission record.");
        }

        var errors = GetSubmissionErrors(contract);
        if (errors.Count > 0)
        {
            return OperationResult<ContractSubmissionResponse>.Failure(
                "submission_validation_failed",
                "Contract does not meet CTR-02 submission requirements. No changes were saved.",
                errors);
        }

        var submittedAtUtc = DateTime.UtcNow;
        contract.Status = ContractStatuses.PendingApproval;
        contract.Submission = new ContractSubmission
        {
            ContractId = contract.Id,
            SubmittedAtUtc = submittedAtUtc
        };

        try
        {
            // EF Core writes the status and simulated hand-off atomically.
            // The shared primary key prevents duplicate submissions.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OperationResult<ContractSubmissionResponse>.Failure(
                "conflict",
                $"Contract {id} has already been submitted or changed by another request.");
        }

        return OperationResult<ContractSubmissionResponse>.Success(
            new ContractSubmissionResponse(
                contract.Id,
                contract.Status,
                submittedAtUtc,
                "Approval hand-off was recorded. Reviewer assignment and approval steps are not implemented in this demo."));
    }

    private async Task<OperationResult<ContractResponse>?> ValidateEditableFieldsAsync(
        SaveContractRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.ContractNo))
        {
            errors.Add("ContractNo is required.");
        }
        else if (request.ContractNo.Trim().Length > 50)
        {
            errors.Add("ContractNo must be 50 characters or fewer.");
        }
        else if (await dbContext.Contracts.AnyAsync(
                     contract => contract.ContractNo == request.ContractNo.Trim() &&
                                 contract.Id != existingId,
                     cancellationToken))
        {
            errors.Add("ContractNo must be unique.");
        }

        if (request.CustomerId <= 0)
        {
            errors.Add("CustomerId must be a positive id.");
        }
        else
        {
            var customerIsActive = await dbContext.Customers.AnyAsync(
                customer => customer.Id == request.CustomerId && customer.Status == "Active",
                cancellationToken);
            if (!customerIsActive)
            {
                errors.Add("CustomerId must reference an existing Active customer.");
            }
        }

        if (request.StartDate == default || request.EndDate == default)
        {
            errors.Add("StartDate and EndDate are required.");
        }
        else if (request.EndDate < request.StartDate)
        {
            errors.Add("EndDate must be on or after StartDate.");
        }

        if (request.ContentSummary?.Length > 4000)
        {
            errors.Add("ContentSummary must be 4000 characters or fewer.");
        }

        if (request.AttachmentFileName?.Length > 255)
        {
            errors.Add("AttachmentFileName must be 255 characters or fewer.");
        }

        return errors.Count == 0
            ? null
            : OperationResult<ContractResponse>.Failure(
                "validation_failed", "Contract data is invalid.", errors);
    }

    private static List<string> GetSubmissionErrors(Contract contract)
    {
        var errors = new List<string>();
        if (contract.Customer is null ||
            !string.Equals(contract.Customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ContractResponse ToResponse(Contract contract) =>
        new(
            contract.Id,
            contract.ContractNo,
            contract.CustomerId,
            contract.Customer?.Name ?? string.Empty,
            contract.StartDate,
            contract.EndDate,
            contract.ContentSummary,
            contract.AttachmentFileName,
            contract.Status,
            contract.Submission?.SubmittedAtUtc);
}
