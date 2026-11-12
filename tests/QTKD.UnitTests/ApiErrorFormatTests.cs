using Microsoft.AspNetCore.Mvc.ModelBinding;
using QTKD.Api.Common;

namespace QTKD.UnitTests;

/// <summary>Regression tests for BUG-02: unreadable requests use the shared error shape.</summary>
public class ApiErrorFormatTests
{
    [Fact]
    public void FromModelState_NamesTheInvalidField_WithoutInternalTypeNames()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("request", "The request field is required.");
        modelState.AddModelError(
            "$.startDate",
            "The JSON value could not be converted to QTKD.Api.Dtos.SaveContractRequest.");

        var error = ApiErrorCodes.FromModelState(modelState);

        Assert.Equal(ApiErrorCodes.InvalidRequest, error.Code);
        var detail = Assert.Single(error.Details!);
        Assert.Equal("startDate: value is missing or has an invalid format.", detail);
        Assert.DoesNotContain("QTKD.Api", detail);
    }

    [Fact]
    public void FromModelState_ReportsInvalidJson()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$", "'b' is an invalid start of a property name.");

        var error = ApiErrorCodes.FromModelState(modelState);

        Assert.Equal(["Request body is not valid JSON."], error.Details);
    }

    [Fact]
    public void FromModelState_ReportsMissingBody()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("request", "The request field is required.");

        var error = ApiErrorCodes.FromModelState(modelState);

        Assert.Equal(["Request body is required and must be valid JSON."], error.Details);
    }
}
