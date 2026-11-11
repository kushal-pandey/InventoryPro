using InventoryPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryPro.Infrastructure.Data;

public static class BenchmarkDataSeeder
{
    private const int ProductCount = 2_000;
    private const int CustomerCount = 500;
    private const int OrderCount = 4_000;
    private const int ItemsPerOrder = 5;
    private const string CategoryName = "Benchmark Catalog";
    private const string FirstSku = "BENCH-000001";

    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Products.AnyAsync(product => product.Sku == FirstSku, cancellationToken))
        {
            var productsAlreadySeeded = await context.Products
                .CountAsync(product => product.Sku.StartsWith("BENCH-"), cancellationToken);
            var customersAlreadySeeded = await context.Customers
                .CountAsync(customer => customer.Email.StartsWith("benchmark-"), cancellationToken);
            var ordersAlreadySeeded = await context.Orders
                .CountAsync(order => order.OrderNumber.StartsWith("BENCH-"), cancellationToken);
            var orderItemsAlreadySeeded = await context.OrderItems
                .CountAsync(item => item.Order.OrderNumber.StartsWith("BENCH-"), cancellationToken);

            if (productsAlreadySeeded == ProductCount &&
                customersAlreadySeeded == CustomerCount &&
                ordersAlreadySeeded == OrderCount &&
                orderItemsAlreadySeeded == OrderCount * ItemsPerOrder)
            {
                return;
            }

            throw new InvalidOperationException(
                "The benchmark dataset is incomplete; use a fresh development database before seeding again.");
        }

        if (await context.Categories.AnyAsync(category => category.Name == CategoryName, cancellationToken))
        {
            throw new InvalidOperationException(
                "The benchmark category already exists without the benchmark product marker; use a fresh development database.");
        }

        var category = new Category
        {
            Name = CategoryName,
            Description = "Generated only for opt-in local API performance measurements."
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);

        var random = new Random(20261109);
        var now = DateTime.UtcNow;
        var products = Enumerable.Range(1, ProductCount)
            .Select(index => new Product
            {
                Name = $"Benchmark Product {index:D4}",
                Sku = $"BENCH-{index:D6}",
                Description = "Generated benchmark catalog item.",
                Price = random.Next(5, 501) + random.Next(0, 100) / 100m,
                StockQuantity = random.Next(10, 500),
                ReorderLevel = random.Next(5, 40),
                CategoryId = category.Id,
                IsActive = random.Next(100) >= 5,
                CreatedAt = now.AddDays(-random.Next(0, 366))
            })
            .ToList();
        context.Products.AddRange(products);

        var customers = Enumerable.Range(1, CustomerCount)
            .Select(index => new Customer
            {
                FullName = $"Benchmark Customer {index:D4}",
                Email = $"benchmark-{index:D4}@inventorypro.test",
                Phone = $"555-{index % 10_000:D4}",
                Address = $"Benchmark address {index:D4}",
                CreatedAt = now.AddDays(-random.Next(0, 730))
            })
            .ToList();
        context.Customers.AddRange(customers);
        await context.SaveChangesAsync(cancellationToken);

        var orders = new List<Order>(OrderCount);
        for (var orderIndex = 0; orderIndex < OrderCount; orderIndex++)
        {
            var orderDate = now.AddDays(-random.Next(0, 366))
                .AddMinutes(-random.Next(0, 24 * 60));
            var statusRoll = random.Next(100);
            var status = statusRoll switch
            {
                < 8 => OrderStatus.Cancelled,
                < 38 => OrderStatus.Pending,
                < 68 => OrderStatus.Confirmed,
                < 88 => OrderStatus.Shipped,
                _ => OrderStatus.Delivered
            };
            var order = new Order
            {
                OrderNumber = $"BENCH-{orderIndex + 1:D6}",
                CustomerId = customers[random.Next(customers.Count)].Id,
                Status = status,
                OrderDate = orderDate,
                CreatedByUserId = "benchmark-seed"
            };

            var selectedProductIndexes = new HashSet<int>();
            while (selectedProductIndexes.Count < ItemsPerOrder)
                selectedProductIndexes.Add(random.Next(products.Count));

            foreach (var productIndex in selectedProductIndexes)
            {
                var product = products[productIndex];
                var quantity = random.Next(1, 6);
                order.OrderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });
                order.TotalAmount += product.Price * quantity;
            }

            orders.Add(order);
        }

        context.Orders.AddRange(orders);
        await context.SaveChangesAsync(cancellationToken);
    }
}
