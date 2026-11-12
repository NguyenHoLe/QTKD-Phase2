using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace QTKD.Api.Common;

/// <summary>Error codes returned in <see cref="ApiErrorResponse.Code"/>.</summary>
public static class ApiErrorCodes
{
    /// <summary>400 — the body could not be read (malformed JSON or wrong value type).</summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>400 — the body was read but business validation failed.</summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>404 — the requested resource does not exist.</summary>
    public const string NotFound = "not_found";

    /// <summary>409 — wrong status for the operation, duplicate or concurrent change.</summary>
    public const string Conflict = "conflict";

    /// <summary>422 — the contract does not meet CTR-02 for submission.</summary>
    public const string SubmissionValidationFailed = "submission_validation_failed";

    /// <summary>500 — unexpected server error; no internal details are returned.</summary>
    public const string InternalError = "internal_error";

    /// <summary>
    /// Builds the error body for requests ASP.NET Core could not bind
    /// (for example <c>"startDate": "abc"</c> or invalid JSON). Replaces the default
    /// ProblemDetails so every error has the same shape and no internal type names leak.
    /// </summary>
    public static ApiErrorResponse FromModelState(ModelStateDictionary modelState)
    {
        var details = new List<string>();
        foreach (var (key, entry) in modelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            if (key.StartsWith('$'))
            {
                // System.Text.Json reports paths such as "$.startDate" or "$" (invalid JSON).
                var field = key.TrimStart('$', '.');
                details.Add(field.Length == 0
                    ? "Request body is not valid JSON."
                    : $"{field}: value is missing or has an invalid format.");
            }
            else if (!string.Equals(key, "request", StringComparison.OrdinalIgnoreCase) &&
                     key.Length > 0)
            {
                details.AddRange(entry.Errors.Select(error => $"{key}: {error.ErrorMessage}"));
            }
        }

        if (details.Count == 0)
        {
            details.Add("Request body is required and must be valid JSON.");
        }

        return new ApiErrorResponse(InvalidRequest, "The request body could not be read.", details);
    }
}
