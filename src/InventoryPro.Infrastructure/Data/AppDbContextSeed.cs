
using InventoryPro.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryPro.Infrastructure.Data;

/// <summary>
/// Seeds application roles, an optional administrator account, and a small
/// starter catalog so the API can be used immediately after setup.
/// Configure INVENTORYPRO_ADMIN_EMAIL and INVENTORYPRO_ADMIN_PASSWORD
/// environment variables to create the initial administrator account.
/// </summary>
public static class AppDbContextSeed
{
    public static async Task SeedAsync(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // Seed application roles.
        string[] roles = { "Admin", "Staff" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(
                    new IdentityRole(role));

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(
                        "; ",
                        roleResult.Errors.Select(error => error.Description));

                    throw new InvalidOperationException(
                        $"Failed to create the '{role}' role: {errors}");
                }
            }
        }

        // Seed an administrator only when credentials are explicitly configured.
        var adminEmail = Environment.GetEnvironmentVariable(
            "INVENTORYPRO_ADMIN_EMAIL");

        var adminPassword = Environment.GetEnvironmentVariable(
            "INVENTORYPRO_ADMIN_PASSWORD");

        if (!string.IsNullOrWhiteSpace(adminEmail) &&
            !string.IsNullOrWhiteSpace(adminPassword))
        {
            var admin = await userManager.FindByEmailAsync(adminEmail);

            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "InventoryPro Admin",
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(
                    admin,
                    adminPassword);

                if (!createResult.Succeeded)
                {
                    var errors = string.Join(
                        "; ",
                        createResult.Errors.Select(error => error.Description));

                    throw new InvalidOperationException(
                        $"Failed to create the administrator account: {errors}");
                }
            }

            // Ensure the configured administrator has the Admin role.
            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                var roleResult = await userManager.AddToRoleAsync(
                    admin,
                    "Admin");

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(
                        "; ",
                        roleResult.Errors.Select(error => error.Description));

                    throw new InvalidOperationException(
                        $"Failed to assign the Admin role: {errors}");
                }
            }
        }

        // Seed starter categories and products when the catalog is empty.
        if (!await context.Categories.AnyAsync())
        {
            var electronics = new Category
            {
                Name = "Electronics",
                Description = "Electronic devices and accessories"
            };

            var stationery = new Category
            {
                Name = "Stationery",
                Description = "Office and school supplies"
            };

            context.Categories.AddRange(electronics, stationery);

            await context.SaveChangesAsync();

            context.Products.AddRange(
                new Product
                {
                    Name = "Wireless Mouse",
                    Sku = "ELEC-001",
                    Description = "2.4GHz wireless mouse",
                    Price = 799.00m,
                    StockQuantity = 50,
                    ReorderLevel = 10,
                    CategoryId = electronics.Id
                },
                new Product
                {
                    Name = "USB-C Cable 1m",
                    Sku = "ELEC-002",
                    Description = "Fast charging cable",
                    Price = 299.00m,
                    StockQuantity = 100,
                    ReorderLevel = 20,
                    CategoryId = electronics.Id
                },
                new Product
                {
                    Name = "A4 Notebook",
                    Sku = "STAT-001",
                    Description = "200 pages, ruled",
                    Price = 89.00m,
                    StockQuantity = 200,
                    ReorderLevel = 30,
                    CategoryId = stationery.Id
                }
            );

            await context.SaveChangesAsync();
        }
    }
}
