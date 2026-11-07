using System.Net;
using CampusGear.Data;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.IntegrationTests;

/// <summary>
/// WBS 43.0 Account Creation &amp; Deactivation and 44.0 Role Assignment: the administrator's Create Account form
/// refuses bad input, and the delete action protects the administrator's own account and the last verified administrator.
/// Runs the real web app against a throwaway SQL Server database (see <see cref="WebAppFixture"/>).
/// </summary>
[Collection("Web app permissions")]
public sealed class AccountRoleManagementTests(WebAppFixture app)
{
    private const string DuplicateEmail = "An account already uses this email address.";
    private const string StaffStudentId = "Student ID Number and department apply to borrowers only.";
    private const string DuplicateStudentId = "This Student ID Number is already registered.";
    private const string OwnAccount = "You cannot delete your own account.";
    private const string LastAdministrator = "Keep at least one active, verified Administrator account.";

    private static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@account.test";

    /// <summary>Submits the Create Account form as the given administrator client.</summary>
    private static Task<HttpResponseMessage> CreateAsync(HttpClient admin, string email, string role,
        string? studentNumber = null, string? department = null)
    {
        var fields = new List<(string, string)>
        {
            ("FirstName", "New"), ("LastName", "Account"), ("Email", email), ("Role", role),
            ("Password", WebAppFixture.Password)
        };
        if (studentNumber is not null) fields.Add(("StudentNumber", studentNumber));
        if (department is not null) fields.Add(("Department", department));
        return WebAppFixture.PostFormAsync(admin, "/admin/users/new", "/admin/users/new", [.. fields]);
    }

    private Task<bool> UserExistsAsync(string email) =>
        app.DbAsync(db => db.Users.AnyAsync(x => x.Email == email));

    /// <summary>Deletes the account as the given administrator, then reads the message shown on the Users page.</summary>
    private static async Task<string> DeleteAsync(HttpClient admin, string userId)
    {
        var response = await WebAppFixture.PostFormAsync(admin, $"/admin/users/{userId}/delete", "/admin/users");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return await admin.GetStringAsync("/admin/users"); // The message is shown once, on the page after the redirect.
    }

    private async Task<HttpClient> NewAdministratorClientAsync(TestAccount administrator) =>
        (await app.SignInAsync(administrator.Email)).Client;

    // ---- 43.0 Account Creation: Create Account errors ----

    [Fact]
    public async Task Create_account_refuses_an_email_that_is_already_in_use()
    {
        var existing = await app.NewAccountAsync("Borrower");
        var admin = app.Client("Administrator");

        // The same email is refused, whatever its letter case, and no second account appears.
        foreach (var email in new[] { existing.Email, existing.Email.ToUpperInvariant() })
        {
            var response = await CreateAsync(admin, email, "Custodian");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); // The form is shown again, with the error.
            Assert.Contains(DuplicateEmail, await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(1, await app.DbAsync(db => db.Users.CountAsync(x => x.Email == existing.Email)));
    }

    [Theory]
    [InlineData("Custodian")]
    [InlineData("Administrator")]
    public async Task Create_account_refuses_a_student_id_or_department_for_a_staff_role(string role)
    {
        var admin = app.Client("Administrator");
        var withStudentId = NewEmail("staff-sid");
        var withDepartment = NewEmail("staff-dept");

        var studentIdResponse = await CreateAsync(admin, withStudentId, role, studentNumber: "25237611");
        Assert.Equal(HttpStatusCode.OK, studentIdResponse.StatusCode);
        Assert.Contains(StaffStudentId, await studentIdResponse.Content.ReadAsStringAsync());

        var departmentResponse = await CreateAsync(admin, withDepartment, role, department: "College of Engineering");
        Assert.Equal(HttpStatusCode.OK, departmentResponse.StatusCode);
        Assert.Contains(StaffStudentId, await departmentResponse.Content.ReadAsStringAsync());

        Assert.False(await UserExistsAsync(withStudentId));
        Assert.False(await UserExistsAsync(withDepartment));

        // Without a Student ID or department the same role is created normally.
        var clean = NewEmail("staff-ok");
        Assert.Equal(HttpStatusCode.Redirect, (await CreateAsync(admin, clean, role)).StatusCode);
        Assert.True(await UserExistsAsync(clean));
    }

    [Fact]
    public async Task Create_account_gives_a_borrower_the_student_id_and_refuses_a_registered_one()
    {
        var admin = app.Client("Administrator");
        var studentId = Guid.NewGuid().ToString("N")[..12];
        var first = NewEmail("borrower-first");

        Assert.Equal(HttpStatusCode.Redirect, (await CreateAsync(admin, first, "Borrower", studentNumber: studentId)).StatusCode);
        Assert.Equal(studentId, await app.DbAsync(db => db.BorrowerProfiles
            .Where(x => x.User.Email == first).Select(x => x.StudentNumber).SingleAsync()));

        var second = NewEmail("borrower-second");
        var response = await CreateAsync(admin, second, "Borrower", studentNumber: studentId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(DuplicateStudentId, await response.Content.ReadAsStringAsync());
        Assert.False(await UserExistsAsync(second));
    }

    // ---- 43.0 Account Deactivation / 44.0 Role Assignment: delete guards ----

    [Fact]
    public async Task Administrator_cannot_delete_their_own_account()
    {
        var administrator = await app.NewAccountAsync("Administrator");
        var admin = await NewAdministratorClientAsync(administrator);

        var page = await DeleteAsync(admin, administrator.UserId);

        Assert.Contains(OwnAccount, page);
        Assert.True(await app.DbAsync(db => db.Users.AnyAsync(x => x.Id == administrator.UserId)));
    }

    [Fact]
    public async Task The_last_active_verified_administrator_cannot_be_deleted()
    {
        var actor = await app.NewAccountAsync("Administrator");
        var target = await app.NewAccountAsync("Administrator");
        var admin = await NewAdministratorClientAsync(actor);

        // An administrator is only "the last one" when no other administrator is both active and verified. The acting
        // administrator normally counts as another, so make every administrator except the target unverified for this
        // check, and put them back afterwards.
        var others = await app.DbAsync(db => db.Users
            .Where(u => u.Id != target.UserId && u.EmailConfirmed &&
                db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Administrator")))
            .Select(u => u.Id).ToListAsync());
        Task SetVerifiedAsync(bool verified) => app.DbAsync(db => db.Users.Where(u => others.Contains(u.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.EmailConfirmed, verified)));

        await SetVerifiedAsync(false);
        try
        {
            var page = await DeleteAsync(admin, target.UserId);
            Assert.Contains(LastAdministrator, page);
            Assert.True(await app.DbAsync(db => db.Users.AnyAsync(x => x.Id == target.UserId)));
        }
        finally
        {
            await SetVerifiedAsync(true);
        }

        // With another verified administrator available again, the same delete goes through,
        // so the guard was the only thing stopping it.
        await DeleteAsync(admin, target.UserId);
        Assert.False(await app.DbAsync(db => db.Users.AnyAsync(x => x.Id == target.UserId)));
    }
}
