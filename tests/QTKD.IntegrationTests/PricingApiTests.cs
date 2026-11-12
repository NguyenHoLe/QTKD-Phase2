using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using QTKD.Api.Common;
using QTKD.Api.Dtos;
using QTKD.IntegrationTests.Infrastructure;

namespace QTKD.IntegrationTests;

/// <summary>PriceList / PriceItem: valid unit price and correct PriceItem–PriceList link.</summary>
[Collection(ApiCollection.Name)]
public class PricingApiTests(QtkdApiFactory factory) : ApiTestBase(factory)
{
    private async Task<PriceListResponse> CreatePriceListAsync()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/price-lists",
            new { name = $"Bảng giá test {NewSuffix()}", effectiveDate = new DateOnly(2026, 11, 1) });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<PriceListResponse>(response);
    }

    [Fact]
    public async Task GetSeedPriceListItems_ReturnsTwoItems()
    {
        var items = await Client.GetFromJsonAsync<List<PriceItemResponse>>("/api/price-lists/1/items");

        Assert.Equal(["SVC-PORT-001", "SVC-PORT-002"], items!.Select(item => item.ServiceCode));
    }

    [Fact]
    public async Task CreatePriceListAndItem_AreStoredAndLinked()
    {
        var priceList = await CreatePriceListAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/price-lists/{priceList.Id}/items",
            new { serviceCode = "SVC-TEST-001", unitPrice = 12345.67m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var db = CreateDbContext();
        var stored = await db.PriceItems.AsNoTracking()
            .SingleAsync(item => item.PriceListId == priceList.Id && item.ServiceCode == "SVC-TEST-001");
        Assert.Equal(12345.67m, stored.UnitPrice);

        var readBack = await Client.GetFromJsonAsync<PriceListResponse>($"/api/price-lists/{priceList.Id}");
        Assert.Equal(1, readBack!.ItemCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddPriceItem_WithNonPositiveUnitPrice_IsRejected(decimal unitPrice)
    {
        var priceList = await CreatePriceListAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/price-lists/{priceList.Id}/items",
            new { serviceCode = "SVC-BAD", unitPrice });

        var error = await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        Assert.Contains("UnitPrice must be greater than zero.", error.Details!);
        await using var db = CreateDbContext();
        Assert.False(await db.PriceItems.AnyAsync(item => item.PriceListId == priceList.Id));
    }

    [Fact]
    public async Task AddDuplicateServiceCode_InSamePriceList_IsRejected()
    {
        var priceList = await CreatePriceListAsync();
        var body = new { serviceCode = "SVC-DUP", unitPrice = 1000m };
        Assert.Equal(
            HttpStatusCode.Created,
            (await Client.PostAsJsonAsync($"/api/price-lists/{priceList.Id}/items", body)).StatusCode);

        var duplicate = await Client.PostAsJsonAsync($"/api/price-lists/{priceList.Id}/items", body);

        await ReadErrorAsync(duplicate, HttpStatusCode.Conflict, ApiErrorCodes.Conflict);
    }

    [Fact]
    public async Task AddPriceItem_ToUnknownPriceList_Returns404()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/price-lists/2147000000/items",
            new { serviceCode = "SVC-X", unitPrice = 1000m });

        await ReadErrorAsync(response, HttpStatusCode.NotFound, ApiErrorCodes.NotFound);
    }

    [Fact]
    public async Task CreatePriceList_WithoutName_IsRejected()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/price-lists",
            new { effectiveDate = new DateOnly(2026, 11, 1) });

        var error = await ReadErrorAsync(response, HttpStatusCode.BadRequest, ApiErrorCodes.ValidationFailed);
        Assert.Contains("Name is required.", error.Details!);
    }
}
