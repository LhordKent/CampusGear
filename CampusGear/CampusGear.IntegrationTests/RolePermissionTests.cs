using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>
/// Proves the permission table in docs/PERMISSIONS.md over real HTTP: for every function, each role that may use it
/// gets a success and every other role gets 403. Anonymous callers are sent to sign in (pages) or get 401.
/// </summary>
[Collection("Web app permissions")]
public sealed class RolePermissionTests(WebAppFixture app)
{
    private static readonly string[] Roles = ["Borrower", "Custodian", "Administrator"];
    private static readonly TimeZoneInfo Manila = ResolveManila();

    // Function (from the function list) → page → roles allowed. B = Borrower, C = Custodian, A = Administrator.
    public static TheoryData<string, string, string> Pages => new()
    {
        { "Borrower Dashboard", "/borrower/home", "B" },
        { "Equipment Reservation", "/borrower/reservation", "B" },
        { "Own borrowing history", "/borrower/history", "B" },
        { "Availability Calendar (borrower, read-only)", "/borrower/calendar", "B" },
        { "Custodian Dashboard", "/custodian/dashboard", "C" },
        { "Approval and Release", "/custodian/approvals", "C" },
        { "Return and Condition Check", "/custodian/returns", "C" },
        { "Borrowing History Report", "/custodian/history", "C" },
        { "Availability Calendar", "/custodian/calendar", "C" },
        { "Equipment categories (read-only)", "/custodian/categories", "C" },
        { "Borrower profiles (read-only)", "/custodian/borrowers", "C" },
        { "Update item status/condition (maintenance)", "/admin/maintenance", "CA" },
        { "Admin Dashboard", "/admin/dashboard", "A" },
        { "User and Role Management", "/admin/users", "A" },
        { "User and Role Management: create account", "/admin/users/new", "A" },
        { "Borrower Profile Management", "/admin/borrowers", "A" },
        { "Equipment Category Management", "/admin/categories", "A" },
        { "Equipment Item Management", "/admin/equipment", "A" },
        { "Audit log", "/admin/audit", "A" },
        { "Borrowing History Report (admin, read)", "/admin/history", "A" },
        { "Availability Calendar (admin, read)", "/admin/calendar", "A" }
    };

    // Function → state-changing endpoint → roles allowed. Ids are random: a forbidden role must be refused before any lookup.
    public static TheoryData<string, string, string> Actions => new()
    {
        { "Equipment Reservation: submit", "/borrower/reservation", "B" },
        { "Own history: cancel request", $"/borrower/reservation/{Guid.NewGuid()}/cancel", "B" },
        { "Approval: approve", $"/custodian/reservations/{Guid.NewGuid()}/approve", "C" },
        { "Approval: reject", $"/custodian/reservations/{Guid.NewGuid()}/reject", "C" },
        { "Release", $"/custodian/reservations/{Guid.NewGuid()}/release", "C" },
        { "Return and condition check", $"/custodian/reservations/{Guid.NewGuid()}/return", "C" },
        { "Maintenance: report for repair", "/admin/maintenance/open", "CA" },
        { "User management: create account", "/admin/users/new", "A" },
        { "User management: edit role/deactivate", "/admin/users/no-such-user/edit", "A" },
        { "User management: delete", "/admin/users/no-such-user/delete", "A" },
        { "Registration approval", "/admin/users/no-such-user/approve", "A" },
        { "Category management: save", "/admin/categories/save", "A" },
        { "Category management: delete", $"/admin/categories/{Guid.NewGuid()}/delete", "A" },
        { "Item management: save", "/admin/equipment/save", "A" },
        { "Item management: delete", $"/admin/equipment/{Guid.NewGuid()}/delete", "A" }
    };

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Pages_open_only_for_their_roles(string function, string path, string allowed)
    {
        foreach (var role in Roles)
        {
            var response = await app.Client(role).GetAsync(path);
            var expected = allowed.Contains(role[0]) ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
            Assert.True(expected == response.StatusCode, $"{function}: {role} GET {path} returned {(int)response.StatusCode}, expected {(int)expected}.");
        }
        var anonymous = await app.NewClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal("/auth/login", PathOf(anonymous.Headers.Location!));
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Actions_are_refused_with_403_for_other_roles(string function, string path, string allowed)
    {
        foreach (var role in Roles.Where(x => !allowed.Contains(x[0])))
        {
            var response = await WebAppFixture.PostFormAsync(app.Client(role), path, Home(role));
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{function}: {role} POST {path} returned {(int)response.StatusCode}, expected 403.");
        }
        var anonymous = await WebAppFixture.PostFormAsync(app.NewClient(), path, "/auth/login");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Forbidden_page_shows_the_access_denied_page_with_403()
    {
        var response = await app.Client("Borrower").GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Access denied", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Anonymous_background_requests_get_401()
    {
        var client = app.NewClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fetch");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/custodian/calendar")).StatusCode);
    }

    [Fact]
    public async Task Removed_admin_borrowing_routes_no_longer_exist()
    {
        foreach (var path in new[] { "/admin/approvals", "/admin/returns", "/admin/reservations" })
            Assert.Equal(HttpStatusCode.NotFound, (await app.Client("Administrator").GetAsync(path)).StatusCode);
    }

    [Fact]
    public void Each_role_lands_on_its_own_dashboard()
    {
        Assert.Equal("/borrower/home", app.LandingPaths["Borrower"]);
        Assert.Equal("/custodian/dashboard", app.LandingPaths["Custodian"]);
        Assert.Equal("/admin/dashboard", app.LandingPaths["Administrator"]);
    }

    [Theory]
    [InlineData("Borrower", "/borrower/home")]
    [InlineData("Custodian", "/custodian/dashboard")]
    [InlineData("Administrator", "/admin/dashboard")]
    public async Task Menus_list_only_pages_the_role_may_open(string role, string home)
    {
        var html = await app.Client(role).GetStringAsync(home);
        var nav = Regex.Match(html, "<nav[^>]*(Workspace|Main) navigation[^>]*>(.*?)</nav>", RegexOptions.Singleline).Value;
        var links = Regex.Matches(nav, "href=\"([^\"]+)\"").Select(x => x.Groups[1].Value).ToList();
        Assert.NotEmpty(links);
        foreach (var link in links)
        {
            var response = await app.Client(role).GetAsync(link);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{role} menu links to {link}, which returned {(int)response.StatusCode}.");
        }
    }

    [Fact]
    public async Task Borrower_reserves_and_only_the_custodian_can_approve()
    {
        var itemId = await app.NewItemAsync();
        var (start, end) = Tomorrow();
        var submitted = await SubmitReservationAsync(app.BorrowerA.Client!, itemId, start, end, "Approval flow test");
        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);
        var id = await app.DbAsync(db => db.Reservations.Where(x => x.EquipmentItemId == itemId).Select(x => x.Id).SingleAsync());

        var adminApprove = await WebAppFixture.PostFormAsync(app.Client("Administrator"), $"/custodian/reservations/{id}/approve", "/admin/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, adminApprove.StatusCode);
        Assert.Equal(ReservationStatus.Pending, await StatusAsync(id));

        var approved = await WebAppFixture.PostFormAsync(app.Client("Custodian"), $"/custodian/reservations/{id}/approve", "/custodian/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);
        Assert.Equal(ReservationStatus.Approved, await StatusAsync(id));

        // Administrators can read the record but are offered no actions on it.
        var adminView = await app.Client("Administrator").GetStringAsync($"/custodian/reservations/{id}");
        var borrowerStudentNumber = await app.DbAsync(db => db.BorrowerProfiles
            .Where(x => x.Id == app.BorrowerA.ProfileId)
            .Select(x => x.StudentNumber)
            .SingleAsync());
        var equipmentName = await app.DbAsync(db => db.EquipmentItems
            .Where(x => x.Id == itemId)
            .Select(x => x.Name)
            .SingleAsync());
        Assert.Contains("borrowing-id-card", adminView);
        Assert.Contains($"RES-{id:N}".ToUpperInvariant(), adminView);
        Assert.Contains("Test Borrower", adminView);
        Assert.Contains(borrowerStudentNumber!, adminView);
        Assert.Contains(equipmentName, adminView);
        Assert.DoesNotContain("/release\"", adminView);
        Assert.DoesNotContain("/approve\"", adminView);
    }

    [Fact]
    public async Task Overlapping_reservations_for_the_same_item_are_refused()
    {
        var itemId = await app.NewItemAsync();
        var (start, end) = Tomorrow();
        await SubmitReservationAsync(app.BorrowerA.Client!, itemId, start, end, "First booking");
        var second = await SubmitReservationAsync(app.BorrowerB.Client!, itemId, start.AddHours(1), end.AddHours(1), "Overlapping booking");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode); // The form is shown again with the conflict.
        Assert.Contains("unavailable", await second.Content.ReadAsStringAsync());
        Assert.Equal(1, await app.DbAsync(db => db.Reservations.CountAsync(x => x.EquipmentItemId == itemId)));
    }

    [Fact]
    public async Task Borrowers_only_see_and_cancel_their_own_records()
    {
        var itemId = await app.NewItemAsync();
        var (start, end) = Tomorrow();
        var purpose = "Private purpose " + Guid.NewGuid().ToString("N")[..8];
        await SubmitReservationAsync(app.BorrowerA.Client!, itemId, start, end, purpose);
        var id = await app.DbAsync(db => db.Reservations.Where(x => x.EquipmentItemId == itemId).Select(x => x.Id).SingleAsync());

        Assert.Contains(purpose, await app.BorrowerA.Client!.GetStringAsync("/borrower/history"));
        Assert.DoesNotContain(purpose, await app.BorrowerB.Client!.GetStringAsync("/borrower/history"));

        var cancelOther = await WebAppFixture.PostFormAsync(app.BorrowerB.Client!, $"/borrower/reservation/{id}/cancel", "/borrower/home");
        Assert.Equal(HttpStatusCode.NotFound, cancelOther.StatusCode);
        Assert.Equal(ReservationStatus.Pending, await StatusAsync(id));

        var cancelOwn = await WebAppFixture.PostFormAsync(app.BorrowerA.Client!, $"/borrower/reservation/{id}/cancel", "/borrower/home");
        Assert.Equal(HttpStatusCode.Redirect, cancelOwn.StatusCode);
        Assert.Equal(ReservationStatus.Cancelled, await StatusAsync(id));
    }

    [Fact]
    public async Task Item_status_flows_available_reserved_borrowed_for_repair_and_blocks_other_moves()
    {
        var itemId = await app.NewItemAsync();
        var now = DateTime.UtcNow;
        var id = await app.NewReservationAsync(app.BorrowerA.ProfileId!.Value, itemId, ReservationStatus.Approved, now.AddHours(-1), now.AddHours(3));
        Assert.Equal(ItemStatus.Reserved, await ItemStatusAsync(itemId));

        // Only the custodian releases; a second release of the same booking is refused.
        Assert.Equal(HttpStatusCode.Forbidden, (await Release("Administrator", id)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Release("Custodian", id)).StatusCode);
        Assert.Equal(ReservationStatus.Released, await StatusAsync(id));
        Assert.Equal(ItemStatus.Borrowed, await ItemStatusAsync(itemId));
        await Release("Custodian", id);
        Assert.Equal(1, await app.DbAsync(db => db.Loans.CountAsync(x => x.ReservationId == id)));

        // Returned damaged: the unit goes for repair; returning again is refused.
        var returned = await WebAppFixture.PostFormAsync(app.Client("Custodian"), $"/custodian/reservations/{id}/return",
            "/custodian/dashboard", ("condition", "Damaged"), ("notes", "Cracked screen"));
        Assert.Equal(HttpStatusCode.Redirect, returned.StatusCode);
        Assert.Equal(ReservationStatus.Completed, await StatusAsync(id));
        Assert.Equal(ItemStatus.ForRepair, await ItemStatusAsync(itemId));
        var again = await app.DbAsync(db => db.Loans.Where(x => x.ReservationId == id).Select(x => x.ReturnedAtUtc).SingleAsync());
        await WebAppFixture.PostFormAsync(app.Client("Custodian"), $"/custodian/reservations/{id}/return", "/custodian/dashboard", ("condition", "Good"));
        Assert.Equal(again, await app.DbAsync(db => db.Loans.Where(x => x.ReservationId == id).Select(x => x.ReturnedAtUtc).SingleAsync()));
        Assert.Equal(ItemStatus.ForRepair, await ItemStatusAsync(itemId));

        Task<HttpResponseMessage> Release(string role, Guid reservationId) => WebAppFixture.PostFormAsync(app.Client(role),
            $"/custodian/reservations/{reservationId}/release", Home(role), ("condition", "Good"));
    }

    [Fact]
    public async Task Administrator_creates_accounts_and_the_custodian_cannot()
    {
        var email = $"created-{Guid.NewGuid():N}@permission.test";
        var fields = new[] { ("FirstName", "Made"), ("LastName", "ByAdmin"), ("Email", email), ("Role", "Custodian"), ("Password", WebAppFixture.Password) };
        Assert.Equal(HttpStatusCode.Forbidden, (await WebAppFixture.PostFormAsync(app.Client("Custodian"), "/admin/users/new", "/custodian/dashboard", fields)).StatusCode);
        Assert.False(await app.DbAsync(db => db.Users.AnyAsync(x => x.Email == email)));

        Assert.Equal(HttpStatusCode.Redirect, (await WebAppFixture.PostFormAsync(app.Client("Administrator"), "/admin/users/new", "/admin/users/new", fields)).StatusCode);
        var role = await app.DbAsync(db => db.UserRoles.Where(ur => db.Users.Any(u => u.Id == ur.UserId && u.Email == email))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).SingleAsync());
        Assert.Equal("Custodian", role);
    }

    [Fact]
    public async Task Administrator_deletes_unused_records_and_deactivates_records_with_history()
    {
        var emptyCategory = await app.DbAsync(async db => { var c = new EquipmentCategory { Name = "Empty " + Guid.NewGuid().ToString("N")[..8] }; db.EquipmentCategories.Add(c); await db.SaveChangesAsync(); return c.Id; });
        var usedCategory = await app.DbAsync(async db => { var c = new EquipmentCategory { Name = "Used " + Guid.NewGuid().ToString("N")[..8] }; db.EquipmentCategories.Add(c); await db.SaveChangesAsync(); return c.Id; });
        var unusedItem = await app.NewItemAsync(usedCategory);
        var usedItem = await app.NewItemAsync(usedCategory);
        var borrower = await app.NewAccountAsync("Borrower");
        var idleBorrower = await app.NewAccountAsync("Borrower");
        await app.NewReservationAsync(borrower.ProfileId!.Value, usedItem, ReservationStatus.Cancelled, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(3));

        var admin = app.Client("Administrator");
        await WebAppFixture.PostFormAsync(admin, $"/admin/categories/{emptyCategory}/delete", "/admin/categories");
        await WebAppFixture.PostFormAsync(admin, $"/admin/categories/{usedCategory}/delete", "/admin/categories");
        await WebAppFixture.PostFormAsync(admin, $"/admin/equipment/{unusedItem}/delete", "/admin/equipment");
        await WebAppFixture.PostFormAsync(admin, $"/admin/equipment/{usedItem}/delete", "/admin/equipment");
        await WebAppFixture.PostFormAsync(admin, $"/admin/users/{idleBorrower.UserId}/delete", "/admin/users");
        await WebAppFixture.PostFormAsync(admin, $"/admin/users/{borrower.UserId}/delete", "/admin/users");

        Assert.False(await app.DbAsync(db => db.EquipmentCategories.AnyAsync(x => x.Id == emptyCategory)));
        Assert.False(await app.DbAsync(db => db.EquipmentCategories.Where(x => x.Id == usedCategory).Select(x => x.IsActive).SingleAsync()));
        Assert.False(await app.DbAsync(db => db.EquipmentItems.AnyAsync(x => x.Id == unusedItem)));
        Assert.False(await app.DbAsync(db => db.EquipmentItems.Where(x => x.Id == usedItem).Select(x => x.IsActive).SingleAsync()));
        Assert.False(await app.DbAsync(db => db.Users.AnyAsync(x => x.Id == idleBorrower.UserId)));
        Assert.False(await app.DbAsync(db => db.Users.Where(x => x.Id == borrower.UserId).Select(x => x.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task Deactivated_users_cannot_sign_in_and_lose_open_sessions()
    {
        var inactive = await app.NewAccountAsync("Borrower", active: false);
        var (_, landing) = await app.SignInAsync(inactive.Email);
        Assert.Equal(string.Empty, landing); // The login form is shown again; no redirect into the app.

        var active = await app.NewAccountAsync("Custodian");
        var (client, _) = await app.SignInAsync(active.Email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/custodian/dashboard")).StatusCode);
        await app.DbAsync(db => db.Users.Where(x => x.Id == active.UserId).ExecuteUpdateAsync(x => x.SetProperty(u => u.IsActive, false)));
        var after = await client.GetAsync("/custodian/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Equal("/auth/login", PathOf(after.Headers.Location!));
    }

    private static string PathOf(Uri location) => location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0];

    private static string Home(string role) => role switch
    {
        "Borrower" => "/borrower/home",
        "Custodian" => "/custodian/dashboard",
        _ => "/admin/dashboard"
    };

    private static (DateTime Start, DateTime End) Tomorrow()
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Manila).Date.AddDays(1).AddHours(9);
        return (local, local.AddHours(2));
    }

    private static Task<HttpResponseMessage> SubmitReservationAsync(HttpClient client, Guid itemId, DateTime startLocal,
        DateTime endLocal, string purpose) => WebAppFixture.PostFormAsync(client, "/borrower/reservation", "/borrower/reservation",
        ("RequestId", Guid.NewGuid().ToString()), ("EquipmentItemId", itemId.ToString()), ("Quantity", "1"),
        ("StartLocal", startLocal.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)),
        ("EndLocal", endLocal.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)), ("Purpose", purpose));

    private Task<ReservationStatus> StatusAsync(Guid id) =>
        app.DbAsync(db => db.Reservations.Where(x => x.Id == id).Select(x => x.Status).SingleAsync());

    private Task<ItemStatus> ItemStatusAsync(Guid itemId) =>
        app.DbAsync(async db => (await ItemStatuses.ForAllAsync(db, DateTime.UtcNow, CancellationToken.None))[itemId]);

    private static TimeZoneInfo ResolveManila()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
    }
}
