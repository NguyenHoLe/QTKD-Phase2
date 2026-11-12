using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Common;
using QTKD.Api.Dtos;
using QTKD.IntegrationTests.Infrastructure;

namespace QTKD.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CustomerApiTests(QtkdApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    [Trait("TC", "TC01")]
    public async Task TC01_CreateValidCustomer_IsStoredAndCanBeReadBack()
    {
        var name = $"Công ty Minh Phát {NewSuffix()}";

        var response = await Client.PostAsJsonAsync("/api/customers", new { name, status = "Active" });

        // HTTP status + response body
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<CustomerResponse>(response);
        Assert.True(created.Id > 0);
        Assert.Equal(name, created.Name);
        Assert.Equal("Active", created.Status);
        Assert.EndsWith($"/api/customers/{created.Id}", response.Headers.Location?.ToString());

        // Data really stored in PostgreSQL
        await using var db = CreateDbContext();
        var stored = await db.Customers.AsNoTracking().SingleAsync(customer => customer.Id == created.Id);
        Assert.Equal(name, stored.Name);
        Assert.Equal("Active", stored.Status);

        // Read back through the API
        var read = await Client.GetFromJsonAsync<CustomerResponse>($"/api/customers/{created.Id}");
        Assert.Equal(created, read);
    }

    [Fact]
    [Trait("TC", "TC01")]
    public async Task TC01_CreateCustomerWithoutStatus_DefaultsToActive()
    {
        var response = await Client.PostAsJsonAsync("/api/customers", new { name = $"Default {NewSuffix()}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<CustomerResponse>(response);
        await using var db = CreateDbContext();
        Assert.Equal("Active", (await db.Customers.FindAsync(created.Id))!.Status);
    }

    public static TheoryData<string> MissingNameBodies => new()
    {
        """{"status":"Active"}""",
        """{"name":null,"status":"Active"}""",
        """{"name":"","status":"Active"}""",
        """{"name":"   ","status":"Active"}"""
    };

    [Theory]
    [Trait("TC", "TC02")]
    [MemberData(nameof(MissingNameBodies))]
    public async Task TC02_CreateCustomerWithoutName_IsRejectedWithMessage_AndNothingIsStored(string body)
    {
        await using var db = CreateDbContext();
        var countBefore = await db.Customers.CountAsync();

        var response = await Client.PostAsync(
            "/api/customers",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var error = await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        Assert.Contains("Name is required.", error.Details!);
        Assert.Equal(countBefore, await db.Customers.CountAsync());
    }

    [Theory]
    [Trait("TC", "TC02")]
    [InlineData("")]
    [InlineData("Pending")]
    public async Task TC02_CreateCustomerWithInvalidStatus_IsRejected(string status)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/customers",
            new { name = $"Invalid status {NewSuffix()}", status });

        var error = await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        Assert.Contains("Status must be Active or Inactive.", error.Details!);
    }

    [Fact]
    [Trait("TC", "TC02")]
    public async Task TC02_CreateCustomerWithNameOver200Characters_IsRejected()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/customers",
            new { name = new string('N', 201), status = "Active" });

        var error = await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        Assert.Contains("Name must be 200 characters or fewer.", error.Details!);
    }

    [Fact]
    [Trait("Bug", "BUG-02")]
    public async Task CreateCustomerWithInvalidJson_ReturnsSharedErrorShape()
    {
        var response = await Client.PostAsync(
            "/api/customers",
            new StringContent("{bad json", Encoding.UTF8, "application/json"));

        var raw = await response.Content.ReadAsStringAsync();
        var error = await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.InvalidRequest);
        Assert.Equal(["Request body is not valid JSON."], error.Details);
        Assert.DoesNotContain("QTKD.Api", raw);
    }

    [Fact]
    public async Task UpdateCustomer_ChangesStoredValues()
    {
        var customer = await CreateCustomerAsync();
        var newName = $"Renamed {NewSuffix()}";

        var response = await Client.PutAsJsonAsync(
            $"/api/customers/{customer.Id}",
            new { name = newName, status = "Inactive" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = CreateDbContext();
        var stored = await db.Customers.AsNoTracking().SingleAsync(item => item.Id == customer.Id);
        Assert.Equal(newName, stored.Name);
        Assert.Equal("Inactive", stored.Status);
    }

    [Fact]
    public async Task UpdateCustomerWithInvalidData_IsRejected_AndStoredValuesStay()
    {
        var customer = await CreateCustomerAsync();

        var response = await Client.PutAsJsonAsync(
            $"/api/customers/{customer.Id}",
            new { name = "", status = "Active" });

        await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        await using var db = CreateDbContext();
        Assert.Equal(customer.Name, (await db.Customers.FindAsync(customer.Id))!.Name);
    }

    [Fact]
    public async Task GetUnknownCustomer_Returns404WithErrorBody()
    {
        var response = await Client.GetAsync("/api/customers/2147000000");

        var error = await ReadErrorAsync(response, HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
        Assert.Equal("Customer 2147000000 was not found.", error.Message);
    }

    [Fact]
    public async Task GetCustomers_ContainsSeedData()
    {
        var customers = await Client.GetFromJsonAsync<List<CustomerResponse>>("/api/customers");

        Assert.Contains(customers!, customer => customer is { Id: 1, Name: "Công ty Minh Phát" });
        Assert.Contains(customers!, customer => customer is { Id: 2, Name: "Công ty An Bình" });
    }
}
