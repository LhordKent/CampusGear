using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>
/// WBS 14.0 Add / Edit Category and 17.0 Add / Edit Equipment Item: the save errors an administrator can hit,
/// and the maintenance case opened when an item is recorded as damaged.
/// </summary>
[Collection("Web app permissions")]
public sealed partial class CategoryItemEditorTests(WebAppFixture app)
{
    private static string Unique(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..10];

    private Task<Guid> NewCategoryAsync(string name, bool active = true) => app.DbAsync(async db =>
    {
        var category = new EquipmentCategory { Name = name, IsActive = active };
        db.EquipmentCategories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    });

    private Task<HttpResponseMessage> SaveCategoryAsync(string name) => WebAppFixture.PostFormAsync(
        app.Client("Administrator"), "/admin/categories/save", "/admin/categories",
        ("Input.Name", name), ("Input.IsActive", "true"));

    /// <summary>
    /// Posts the Add Equipment form as multipart, the way the browser sends it when a photo is attached.
    /// </summary>
    private async Task<HttpResponseMessage> SaveEquipmentAsync(string assetCode, Guid categoryId,
        string condition = "Good", byte[]? photo = null)
    {
        var client = app.Client("Administrator");
        var page = await client.GetStringAsync("/admin/equipment");
        var token = TokenPattern().Match(page).Groups[1].Value;
        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(categoryId.ToString()), "Input.CategoryId" },
            { new StringContent(assetCode), "Input.AssetCode" },
            { new StringContent("Editor test item " + assetCode), "Input.Name" },
            { new StringContent(condition), "Input.Condition" },
            { new StringContent("true"), "Input.IsActive" }
        };
        if (photo is not null)
        {
            var file = new ByteArrayContent(photo);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "imageFile", "photo.png");
        }
        return await client.PostAsync("/admin/equipment/save", form);
    }

    private Task<bool> ItemExistsAsync(string assetCode) =>
        app.DbAsync(db => db.EquipmentItems.AnyAsync(x => x.AssetCode == assetCode));

    // WBS 14.0 — Add / Edit Category

    [Fact]
    public async Task AddCategory_WithNewName_SavesCategory()
    {
        var name = Unique("NewCategory");

        var response = await SaveCategoryAsync(name);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await app.DbAsync(db => db.EquipmentCategories.AnyAsync(x => x.Name == name)));
    }

    [Fact]
    public async Task AddCategory_WithDuplicateName_ShowsErrorAndSavesNothing()
    {
        var name = Unique("DuplicateCategory");
        await NewCategoryAsync(name);

        var response = await SaveCategoryAsync(name);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("That category name already exists", html);
        Assert.Equal(1, await app.DbAsync(db => db.EquipmentCategories.CountAsync(x => x.Name == name)));
    }

    [Fact]
    public async Task AddCategory_WithDuplicateNameInDifferentCase_IsRefused()
    {
        var name = Unique("CaseCategory");
        await NewCategoryAsync(name);

        var response = await SaveCategoryAsync(name.ToUpperInvariant());
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("That category name already exists", html);
    }

    [Fact]
    public async Task DeactivateCategory_WithActiveEquipment_IsRefused()
    {
        var name = Unique("BusyCategory");
        var categoryId = await NewCategoryAsync(name);
        await app.NewItemAsync(categoryId);
        var rowVersion = await app.DbAsync(async db => Convert.ToBase64String(
            (await db.EquipmentCategories.SingleAsync(x => x.Id == categoryId)).RowVersion));

        var response = await WebAppFixture.PostFormAsync(app.Client("Administrator"),
            "/admin/categories/save", "/admin/categories",
            ("Input.Id", categoryId.ToString()), ("Input.Name", name),
            ("Input.IsActive", "false"), ("Input.RowVersion", rowVersion));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("before deactivating the category", html);
        Assert.True(await app.DbAsync(async db => (await db.EquipmentCategories.SingleAsync(x => x.Id == categoryId)).IsActive));
    }

    // WBS 17.0 — Add / Edit Equipment Item

    [Fact]
    public async Task AddEquipment_InInactiveCategory_ShowsErrorAndSavesNothing()
    {
        var categoryId = await NewCategoryAsync(Unique("InactiveCategory"), active: false);
        var assetCode = Unique("INACT");

        var response = await SaveEquipmentAsync(assetCode, categoryId);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Choose an active category.", html);
        Assert.False(await ItemExistsAsync(assetCode));
    }

    [Fact]
    public async Task AddEquipment_WithPhotoOver5MB_ShowsErrorAndSavesNothing()
    {
        var assetCode = Unique("BIGPIC");
        var photo = new byte[5 * 1024 * 1024 + 1];

        var response = await SaveEquipmentAsync(assetCode, app.CategoryId, photo: photo);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Use an image of 5 MB or smaller.", html);
        Assert.False(await ItemExistsAsync(assetCode));
    }

    [Fact]
    public async Task AddEquipment_WithFileThatIsNotAnImage_ShowsErrorAndSavesNothing()
    {
        var assetCode = Unique("NOTIMG");
        // Named photo.png and sent as image/png, but the bytes are plain text, so the type check must refuse it.
        var photo = "this is not an image"u8.ToArray();

        var response = await SaveEquipmentAsync(assetCode, app.CategoryId, photo: photo);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Upload a JPEG, PNG, WebP or GIF image.", html);
        Assert.False(await ItemExistsAsync(assetCode));
    }

    [Fact]
    public async Task AddEquipment_WithDuplicateAssetCode_ShowsErrorAndSavesNothing()
    {
        var existingId = await app.NewItemAsync();
        var assetCode = await app.DbAsync(db =>
            db.EquipmentItems.Where(x => x.Id == existingId).Select(x => x.AssetCode).SingleAsync());

        var response = await SaveEquipmentAsync(assetCode, app.CategoryId);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The asset code or serial number already exists", html);
        Assert.Equal(1, await app.DbAsync(db => db.EquipmentItems.CountAsync(x => x.AssetCode == assetCode)));
    }

    [Theory]
    [InlineData("Damaged")]
    [InlineData("Unserviceable")]
    public async Task AddEquipment_RecordedAsDamaged_OpensMaintenanceCase(string condition)
    {
        var assetCode = Unique("DMG");

        var response = await SaveEquipmentAsync(assetCode, app.CategoryId, condition);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var (onHold, openCases) = await app.DbAsync(async db =>
        {
            var item = await db.EquipmentItems.SingleAsync(x => x.AssetCode == assetCode);
            var cases = await db.MaintenanceCases.CountAsync(x => x.EquipmentItemId == item.Id && x.Status == MaintenanceStatus.Open);
            return (item.IsMaintenanceHold, cases);
        });
        Assert.True(onHold);
        Assert.Equal(1, openCases);
    }

    [Fact]
    public async Task AddEquipment_InGoodCondition_OpensNoMaintenanceCase()
    {
        var assetCode = Unique("GOOD");

        var response = await SaveEquipmentAsync(assetCode, app.CategoryId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var (onHold, anyCase) = await app.DbAsync(async db =>
        {
            var item = await db.EquipmentItems.SingleAsync(x => x.AssetCode == assetCode);
            return (item.IsMaintenanceHold, await db.MaintenanceCases.AnyAsync(x => x.EquipmentItemId == item.Id));
        });
        Assert.False(onHold);
        Assert.False(anyCase);
    }

    // Modal editors: the dialog is loaded on its own and saves with fetch, answering in JSON.

    [Theory]
    [InlineData("/admin/categories?form=add", "category-editor")]
    [InlineData("/admin/equipment?form=add", "equipment-editor")]
    [InlineData("/admin/users/new", "account-create")]
    public async Task Editor_requested_by_a_dialog_returns_only_the_dialog(string path, string dialogId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Modal", "1");

        var response = await app.Client("Administrator").SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("<dialog id=\"" + dialogId + "\"", html.TrimStart());
        Assert.DoesNotContain("<main", html);
    }

    [Fact]
    public async Task Editor_opened_directly_shows_the_list_with_the_dialog_over_it()
    {
        var html = await app.Client("Administrator").GetStringAsync("/admin/categories?form=add");

        Assert.Contains("id=\"category-results\"", html);
        Assert.Contains("<dialog id=\"category-editor\"", html);
    }

    [Fact]
    public async Task AddCategory_FromDialog_AnswersJson()
    {
        var name = Unique("DialogCategory");

        var saved = await PostCategoryWithFetchAsync(name);
        var duplicate = await PostCategoryWithFetchAsync(name);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using (var json = System.Text.Json.JsonDocument.Parse(await saved.Content.ReadAsStringAsync()))
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal((HttpStatusCode)422, duplicate.StatusCode);
        using (var json = System.Text.Json.JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync()))
        {
            Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Contains("That category name already exists", json.RootElement.GetProperty("message").GetString());
        }
        Assert.Equal(1, await app.DbAsync(db => db.EquipmentCategories.CountAsync(x => x.Name == name)));
    }

    private async Task<HttpResponseMessage> PostCategoryWithFetchAsync(string name)
    {
        var client = app.Client("Administrator");
        var token = TokenPattern().Match(await client.GetStringAsync("/admin/categories?form=add")).Groups[1].Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/categories/save")
        {
            Content = new FormUrlEncodedContent([new("__RequestVerificationToken", token), new("Input.Name", name), new("Input.IsActive", "true")])
        };
        request.Headers.Add("X-Requested-With", "fetch");
        return await client.SendAsync(request);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();
}
