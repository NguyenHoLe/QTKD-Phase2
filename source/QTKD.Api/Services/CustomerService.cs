using Microsoft.EntityFrameworkCore;
using QTKD.Api.Data;
using QTKD.Api.Dtos;

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
}
