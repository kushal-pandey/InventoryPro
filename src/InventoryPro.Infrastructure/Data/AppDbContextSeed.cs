using InventoryPro.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace InventoryPro.Infrastructure.Data;

/// <summary>
/// Seeds roles, a default admin user, and a small starter catalog so the API is
/// immediately usable after the first run (via `dotnet run`).
/// </summary>
public static class AppDbContextSeed
{
    public static async Task SeedAsync(AppDbContext context, UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        string[] roles = { "Admin", "Staff" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        if (await userManager.FindByEmailAsync("admin@inventorypro.local") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@inventorypro.local",
                Email = "admin@inventorypro.local",
                FullName = "Default Admin",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, "Admin@12345");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }

        if (!context.Categories.Any())
        {
            var electronics = new Category { Name = "Electronics", Description = "Electronic devices and accessories" };
            var stationery = new Category { Name = "Stationery", Description = "Office and school supplies" };

            context.Categories.AddRange(electronics, stationery);
            await context.SaveChangesAsync();

            context.Products.AddRange(
                new Product
                {
                    Name = "Wireless Mouse", Sku = "ELEC-001", Description = "2.4GHz wireless mouse",
                    Price = 799.00m, StockQuantity = 50, ReorderLevel = 10, CategoryId = electronics.Id
                },
                new Product
                {
                    Name = "USB-C Cable 1m", Sku = "ELEC-002", Description = "Fast charging cable",
                    Price = 299.00m, StockQuantity = 100, ReorderLevel = 20, CategoryId = electronics.Id
                },
                new Product
                {
                    Name = "A4 Notebook", Sku = "STAT-001", Description = "200 pages, ruled",
                    Price = 89.00m, StockQuantity = 200, ReorderLevel = 30, CategoryId = stationery.Id
                }
            );
            await context.SaveChangesAsync();
        }
    }
}
