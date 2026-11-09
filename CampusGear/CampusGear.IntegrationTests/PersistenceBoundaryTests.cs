using CampusGear.Data;
using CampusGear.Data.Interfaces;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace CampusGear.IntegrationTests;
[Collection("Web app permissions")]
public sealed class PersistenceBoundaryTests(WebAppFixture app)
{
    [Fact]
    public async Task Identity_repository_and_audit_writes_share_the_unit_of_work_transaction()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var email = Guid.NewGuid() + "@rollback.integration.test";
        var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Rollback", LastName = "Check" };
        await using (var transaction = await work.BeginAsync())
        {
            Assert.True((await users.CreateAsync(user, "Rollback-Check!42")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Borrower")).Succeeded);
            scope.ServiceProvider.GetRequiredService<IAccountRepository>().AddProfile(new() { UserId = user.Id });
            scope.ServiceProvider.GetRequiredService<IAuditRepository>().Add(new()
            {
                ActorUserId = app.Administrator.UserId,
                Action = "transaction.check",
                EntityType = "ApplicationUser",
                EntityId = user.Id,
                Outcome = "Succeeded"
            });
            await work.SaveAsync();
            await transaction.RollbackAsync();
        }
        work.ClearTracking();
        Assert.False(await app.DbAsync(db => db.Users.AnyAsync(x => x.Id == user.Id)));
        Assert.False(await app.DbAsync(db => db.BorrowerProfiles.AnyAsync(x => x.UserId == user.Id)));
        Assert.False(await app.DbAsync(db => db.AuditEvents.AnyAsync(x => x.EntityId == user.Id)));
    }
    [Fact]
    public async Task Foreign_key_failure_is_sanitized_inside_Data_without_committing_the_item()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = new EquipmentItem { CategoryId = Guid.NewGuid(), AssetCode = "Boundary-" + Guid.NewGuid(), Name = "Not persisted", Condition = EquipmentCondition.Good };
        db.EquipmentItems.Add(item);
        var error = await Assert.ThrowsAsync<PersistenceException>(() => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveAsync());
        Assert.Equal(PersistenceFailure.Constraint, error.Failure);
        Assert.Equal("Constraint", error.Message); Assert.Null(error.InnerException);
        Assert.False(await app.DbAsync(data => data.EquipmentItems.AnyAsync(x => x.Id == item.Id)));
    }
}
