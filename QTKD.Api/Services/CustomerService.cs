using Microsoft.EntityFrameworkCore;
using QTKD.Api.Common;
using QTKD.Api.Data;
using QTKD.Api.Dtos;
using QTKD.Api.Models;

namespace QTKD.Api.Services;

public sealed class CustomerService(QtkdDbContext dbContext)
{
    public Task<List<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Id)
            .Select(customer => new CustomerResponse(
                customer.Id,
                customer.Name,
                customer.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == id)
            .Select(customer => new CustomerResponse(
                customer.Id,
                customer.Name,
                customer.Status))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OperationResult<CustomerResponse>> CreateAsync(
        SaveCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var customer = new Customer
        {
            Name = request.Name!.Trim(),
            Status = NormalizeStatus(request.Status)
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<CustomerResponse>.Success(ToResponse(customer));
    }

    public async Task<OperationResult<CustomerResponse>> UpdateAsync(
        int id,
        SaveCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (customer is null)
        {
            return OperationResult<CustomerResponse>.Failure(
                ApiErrorCodes.NotFound, $"Customer {id} was not found.");
        }

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return validationError;
        }

        customer.Name = request.Name!.Trim();
        customer.Status = NormalizeStatus(request.Status);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<CustomerResponse>.Success(ToResponse(customer));
    }

    private static OperationResult<CustomerResponse>? Validate(SaveCustomerRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Name is required.");
        }
        else if (request.Name.Trim().Length > 200)
        {
            errors.Add("Name must be 200 characters or fewer.");
        }

        if (!string.Equals(request.Status, CustomerStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Status, CustomerStatuses.Inactive, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Status must be Active or Inactive.");
        }

        return errors.Count == 0
            ? null
            : OperationResult<CustomerResponse>.Failure(
                ApiErrorCodes.ValidationFailed, "Customer data is invalid.", errors);
    }

    private static string NormalizeStatus(string? status) =>
        string.Equals(status, CustomerStatuses.Inactive, StringComparison.OrdinalIgnoreCase)
            ? CustomerStatuses.Inactive
            : CustomerStatuses.Active;

    private static CustomerResponse ToResponse(Customer customer) =>
        new(customer.Id, customer.Name, customer.Status);
}
