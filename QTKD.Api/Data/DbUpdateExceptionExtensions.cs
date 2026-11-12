using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace QTKD.Api.Data;

public static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// True when PostgreSQL rejected the write because of a unique index or primary key
    /// (SQLSTATE 23505). This happens when two concurrent requests pass the
    /// application-level duplicate check at the same time.
    /// </summary>
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
}
