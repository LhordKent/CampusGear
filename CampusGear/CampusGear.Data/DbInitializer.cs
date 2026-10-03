using CampusGear.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusGear.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, bool migrate)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (migrate)
            await db.Database.MigrateAsync();

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Borrower", "Custodian", "Administrator" })
        {
            if (await roles.RoleExistsAsync(role)) continue;
            var created = await roles.CreateAsync(new IdentityRole(role));
            if (!created.Succeeded)
                throw new InvalidOperationException($"Could not create {role} role: {string.Join(", ", created.Errors.Select(e => e.Description))}");
        }

        if (migrate && !await db.EquipmentItems.AnyAsync())
        {
            var computers = await GetOrCreateCategoryAsync(db, "Computers", "Portable computing equipment");
            var media = await GetOrCreateCategoryAsync(db, "Media equipment", "Cameras and presentation devices");
            db.EquipmentItems.AddRange(
                new EquipmentItem { Category = computers, AssetCode = "EQ-001", Name = "Laptop Dell 5420", SerialNumber = "DEMO-LAPTOP-001", Location = "Equipment Room A" },
                new EquipmentItem { Category = media, AssetCode = "EQ-002", Name = "Canon EOS Camera", SerialNumber = "DEMO-CAMERA-002", Location = "Equipment Room A" },
                new EquipmentItem { Category = media, AssetCode = "EQ-003", Name = "Epson Projector", SerialNumber = "DEMO-PROJECTOR-003", Location = "Equipment Room B" });
            await db.SaveChangesAsync();
        }

        var email = Environment.GetEnvironmentVariable("CAMPUSGEAR_BOOTSTRAP_ADMIN_EMAIL");
        var password = Environment.GetEnvironmentVariable("CAMPUSGEAR_BOOTSTRAP_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.GetUsersInRoleAsync("Administrator") is { Count: > 0 }) return;
        if (await users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("Bootstrap administrator email already belongs to an account.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = Environment.GetEnvironmentVariable("CAMPUSGEAR_BOOTSTRAP_ADMIN_NAME") ?? "CampusGear Administrator"
        };
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not create bootstrap administrator: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        result = await users.AddToRoleAsync(user, "Administrator");
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not assign administrator role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        await transaction.CommitAsync();
    }

    private static async Task<EquipmentCategory> GetOrCreateCategoryAsync(
        ApplicationDbContext db, string name, string description)
    {
        var existing = await db.EquipmentCategories.FirstOrDefaultAsync(x => x.Name == name);
        if (existing is not null) return existing;
        var category = new EquipmentCategory { Name = name, Description = description };
        db.EquipmentCategories.Add(category);
        return category;
    }
}
