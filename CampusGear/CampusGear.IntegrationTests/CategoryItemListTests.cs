
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CampusGear.IntegrationTests;

[Collection("Web app permissions")]
public sealed class CategoryItemListTests(WebAppFixture app)
{
    private async Task<Guid> NewCategoryAsync(string name, string? description = null)
    {
        return await app.DbAsync(async db =>
        {
            var category = new EquipmentCategory
            {
                Name = name,
                Description = description
            };

            db.EquipmentCategories.Add(category);
            await db.SaveChangesAsync();

            return category.Id;
        });
    }

    // WBS 13.0 — Category List

    [Fact]
    public async Task CategoryList_SearchesByName()
    {
        var name = "CategorySearch-" + Guid.NewGuid().ToString("N");
        await NewCategoryAsync(name);

        var response = await app.Client("Administrator")
            .GetAsync("/admin/categories?q=" + Uri.EscapeDataString(name));

        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains(name, html);
    }

    [Fact]
    public async Task CategoryList_SearchesByDescription()
    {
        var description = "DescriptionSearch-" + Guid.NewGuid().ToString("N");
        await NewCategoryAsync("Description test category", description);

        var response = await app.Client("Administrator")
            .GetAsync("/admin/categories?q=" + Uri.EscapeDataString(description));

        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains(description, html);
    }

    [Fact]
    public async Task CategoryList_ShowsNoMatchingCategories()
    {
        var response = await app.Client("Administrator")
            .GetAsync("/admin/categories?q=NoSuchCategory-" + Guid.NewGuid().ToString("N"));

        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("No matching categories", html);
    }

    [Fact]
    public async Task DeleteCategoryWithEquipment_DeactivatesCategory()
    {
        var categoryId = await NewCategoryAsync(
            "CategoryInUse-" + Guid.NewGuid().ToString("N"));

        var itemId = await app.NewItemAsync(categoryId);

        var response = await WebAppFixture.PostFormAsync(
            app.Client("Administrator"),
            $"/admin/categories/{categoryId}/delete",
            "/admin/categories");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        var result = await app.DbAsync(async db =>
        {
            var category = await db.EquipmentCategories
                .SingleAsync(x => x.Id == categoryId);

            var item = await db.EquipmentItems
                .SingleAsync(x => x.Id == itemId);

            return (category.IsActive, item.CategoryId);
        });

        Assert.False(result.IsActive);
        Assert.Equal(categoryId, result.CategoryId);
    }

    [Fact]
    public async Task DeleteEmptyCategory_RemovesCategory()
    {
        var categoryId = await NewCategoryAsync(
            "EmptyCategory-" + Guid.NewGuid().ToString("N"));

        var response = await WebAppFixture.PostFormAsync(
            app.Client("Administrator"),
            $"/admin/categories/{categoryId}/delete",
            "/admin/categories");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        var exists = await app.DbAsync(db =>
            db.EquipmentCategories.AnyAsync(x => x.Id == categoryId));

        Assert.False(exists);
    }

    // WBS 16.0 — Equipment Item List

    [Fact]
    public async Task EquipmentList_SearchesByName()
    {
        var itemId = await app.NewItemAsync();

        var name = await app.DbAsync(async db =>
            await db.EquipmentItems
                .Where(x => x.Id == itemId)
                .Select(x => x.Name)
                .SingleAsync());

        var response = await app.Client("Administrator")
            .GetAsync("/admin/equipment?q=" + Uri.EscapeDataString(name));

        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains(name, html);
    }

    [Fact]
    public async Task EquipmentList_SearchesByAssetCode()
    {
        var itemId = await app.NewItemAsync();

        var assetCode = await app.DbAsync(async db =>
            await db.EquipmentItems
                .Where(x => x.Id == itemId)
                .Select(x => x.AssetCode)
                .SingleAsync());

        var response = await app.Client("Administrator")
            .GetAsync("/admin/equipment?q=" + Uri.EscapeDataString(assetCode));

        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains(assetCode, html);
    }

    [Fact]
    public async Task EquipmentList_ShowsNoMatchingEquipment()
    {
        var response = await app.Client("Administrator")
            .GetAsync("/admin/equipment?q=NoSuchEquipment-" + Guid.NewGuid().ToString("N"));

        var html = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("No matching equipment", html);
    }

    [Fact]
    public async Task EquipmentList_PaginatesAndPreservesSearchQuery()
    {
        var query = "PaginationTest-" + Guid.NewGuid().ToString("N");

        await app.DbAsync(async db =>
        {
            for (var i = 0; i < 16; i++)
            {
                db.EquipmentItems.Add(new EquipmentItem
                {
                    CategoryId = app.CategoryId,
                    Name = $"{query}-Item-{i:D2}",
                    AssetCode = "PG-" + Guid.NewGuid().ToString("N")[..10],
                    Condition = EquipmentCondition.Good
                });
            }

            await db.SaveChangesAsync();
            return true;
        });

        var client = app.Client("Administrator");

        var firstResponse = await client.GetAsync(
            "/admin/equipment?q=" + Uri.EscapeDataString(query));

        var firstHtml = await firstResponse.Content.ReadAsStringAsync();

        Assert.True(firstResponse.IsSuccessStatusCode);
        Assert.Contains("Page 1 of 2", firstHtml);
        Assert.Contains($"q={Uri.EscapeDataString(query)}&amp;page=2", firstHtml);

        var secondResponse = await client.GetAsync(
            "/admin/equipment?q=" + Uri.EscapeDataString(query) + "&page=2");

        var secondHtml = await secondResponse.Content.ReadAsStringAsync();

        Assert.True(secondResponse.IsSuccessStatusCode);
        Assert.Contains("Page 2 of 2", secondHtml);
        Assert.Contains($"{query}-Item-", secondHtml);
        Assert.DoesNotContain("Next", secondHtml);
    }

    [Fact]
    public async Task DeleteEquipmentWithReservationHistory_DeactivatesItem()
    {
        var itemId = await app.NewItemAsync();

        await app.NewReservationAsync(
            app.BorrowerA.ProfileId!.Value,
            itemId,
            ReservationStatus.Pending,
            DateTime.UtcNow.AddDays(2),
            DateTime.UtcNow.AddDays(3));

        var response = await WebAppFixture.PostFormAsync(
            app.Client("Administrator"),
            $"/admin/equipment/{itemId}/delete",
            "/admin/equipment");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        var result = await app.DbAsync(async db =>
        {
            var item = await db.EquipmentItems
                .SingleAsync(x => x.Id == itemId);

            var reservationExists = await db.Reservations
                .AnyAsync(x => x.EquipmentItemId == itemId);

            return (item.IsActive, reservationExists);
        });

        Assert.False(result.IsActive);
        Assert.True(result.reservationExists);
    }

    [Fact]
    public async Task DeleteEquipmentWithoutHistory_RemovesItem()
    {
        var itemId = await app.NewItemAsync();

        var response = await WebAppFixture.PostFormAsync(
            app.Client("Administrator"),
            $"/admin/equipment/{itemId}/delete",
            "/admin/equipment");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);

        var exists = await app.DbAsync(db =>
            db.EquipmentItems.AnyAsync(x => x.Id == itemId));

        Assert.False(exists);
    }
}
