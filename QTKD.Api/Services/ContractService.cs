using Microsoft.EntityFrameworkCore;
using QTKD.Api.Data;
using QTKD.Api.Dtos;

namespace QTKD.Api.Services;

public sealed class ContractService(QtkdDbContext dbContext)
{
    public Task<ContractResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.Contracts
            .AsNoTracking()
            .Where(contract => contract.Id == id)
            .Select(contract => new ContractResponse(
                contract.Id,
                contract.ContractNo,
                contract.CustomerId,
                contract.Customer!.Name,
                contract.StartDate,
                contract.EndDate,
                contract.Status))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
