using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace InventoryPro.IntegrationTests;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task Login_accepts_seeded_admin_and_rejects_wrong_password()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();

        using var success = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiTestHost.AdminEmail,
            password = ApiTestHost.AdminPassword
        });
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        using var failure = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiTestHost.AdminEmail,
            password = "WrongPassword!2026"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
    }

    [Fact]
    public async Task Login_locks_admin_after_five_failed_attempts()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = ApiTestHost.AdminEmail,
                password = "WrongPassword!2026"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var fifthFailure = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiTestHost.AdminEmail,
            password = "WrongPassword!2026"
        });
        Assert.Equal(HttpStatusCode.Locked, fifthFailure.StatusCode);

        using var locked = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiTestHost.AdminEmail,
            password = ApiTestHost.AdminPassword
        });
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
        await AssertApiErrorShapeAsync(locked, 423);
    }

    [Fact]
    public async Task Registration_is_private_but_admin_can_create_staff()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();

        using var status = await client.GetAsync("/api/auth/registration-status");
        using var statusBody = await JsonDocument.ParseAsync(await status.Content.ReadAsStreamAsync());
        Assert.False(statusBody.RootElement.GetProperty("enabled").GetBoolean());

        using var publicRegistration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Public User",
            email = "public@integration.test",
            password = "StaffAccount!2026"
        });
        Assert.Equal(HttpStatusCode.Forbidden, publicRegistration.StatusCode);
        await AssertApiErrorShapeAsync(publicRegistration, 403);

        await SetAdminTokenAsync(client);
        using var adminRegistration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Staff User",
            email = "staff@integration.test",
            password = "StaffAccount!2026"
        });
        Assert.Equal(HttpStatusCode.OK, adminRegistration.StatusCode);
        using var body = await JsonDocument.ParseAsync(await adminRegistration.Content.ReadAsStreamAsync());
        Assert.Contains("Staff", body.RootElement.GetProperty("roles").EnumerateArray()
            .Select(role => role.GetString()));
    }

    [Fact]
    public async Task Public_registration_can_be_enabled_for_development_and_creates_staff_accounts()
    {
        using var host = new ApiTestHost(allowPublicRegistration: true);
        using var client = host.CreateClient();

        using var status = await client.GetAsync("/api/auth/registration-status");
        using var statusBody = await JsonDocument.ParseAsync(await status.Content.ReadAsStreamAsync());
        Assert.True(statusBody.RootElement.GetProperty("enabled").GetBoolean());

        const string email = "self-registered@integration.test";
        const string password = "SelfRegistered!2026";
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Self Registered Staff",
            email,
            password
        });

        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        using var registrationBody = await JsonDocument.ParseAsync(await registration.Content.ReadAsStreamAsync());
        Assert.Contains("Staff", registrationBody.RootElement.GetProperty("roles").EnumerateArray()
            .Select(role => role.GetString()));

        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_and_create_users_and_role_changes_revoke_old_tokens()
    {
        using var host = new ApiTestHost();
        using var adminClient = host.CreateClient();
        await SetAdminTokenAsync(adminClient);
        var adminToken = adminClient.DefaultRequestHeaders.Authorization!.Parameter!;

        using var selfList = await adminClient.GetAsync("/api/users?pageNumber=1&pageSize=10");
        using var selfBody = await JsonDocument.ParseAsync(await selfList.Content.ReadAsStreamAsync());
        var adminId = selfBody.RootElement.GetProperty("items").EnumerateArray()
            .Single(user => user.GetProperty("email").GetString() == ApiTestHost.AdminEmail)
            .GetProperty("id").GetString()!;
        using var ownRole = await adminClient.PatchAsJsonAsync(
            $"/api/users/{adminId}/role", new { role = "Staff" });
        using var ownDeactivation = await adminClient.PatchAsJsonAsync(
            $"/api/users/{adminId}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, ownRole.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, ownDeactivation.StatusCode);
        await AssertApiErrorShapeAsync(ownRole, 409);
        await AssertApiErrorShapeAsync(ownDeactivation, 409);

        using var weakPassword = await adminClient.PostAsJsonAsync("/api/users", new
        {
            fullName = "Weak Password",
            email = "weak-password@integration.test",
            password = "weak",
            role = "Staff"
        });
        Assert.Equal(HttpStatusCode.BadRequest, weakPassword.StatusCode);
        await AssertApiErrorShapeAsync(weakPassword, 400);

        using var createdAdmin = await adminClient.PostAsJsonAsync("/api/users", new
        {
            fullName = "Managed Admin",
            email = "managed-admin@integration.test",
            password = "ManagedAdmin!2026",
            role = "Admin"
        });
        Assert.Equal(HttpStatusCode.Created, createdAdmin.StatusCode);
        using var createdAdminBody = await JsonDocument.ParseAsync(await createdAdmin.Content.ReadAsStreamAsync());
        Assert.Contains("Admin", createdAdminBody.RootElement.GetProperty("roles").EnumerateArray()
            .Select(role => role.GetString()));

        const string staffEmail = "managed-staff@integration.test";
        const string password = "ManagedStaff!2026";
        using var created = await adminClient.PostAsJsonAsync("/api/users", new
        {
            fullName = "Managed Staff",
            email = staffEmail,
            password,
            role = "Staff"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdBody = await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync());
        var staffId = createdBody.RootElement.GetProperty("id").GetString()!;
        Assert.Equal("Staff", Assert.Single(
            createdBody.RootElement.GetProperty("roles").EnumerateArray()).GetString());
        Assert.True(created.Headers.Location?.ToString().EndsWith($"/api/users/{staffId}", StringComparison.Ordinal));

        using var cappedList = await adminClient.GetAsync("/api/users?pageSize=500");
        using var cappedBody = await JsonDocument.ParseAsync(await cappedList.Content.ReadAsStreamAsync());
        Assert.Equal(100, cappedBody.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Contains(cappedBody.RootElement.GetProperty("items").EnumerateArray(), user =>
            user.GetProperty("email").GetString() == staffEmail);

        using var staffClient = host.CreateClient();
        using var login = await staffClient.PostAsJsonAsync("/api/auth/login", new { email = staffEmail, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginBody = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        var oldStaffToken = loginBody.RootElement.GetProperty("token").GetString()!;
        SetToken(staffClient, oldStaffToken);

        using var staffList = await staffClient.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, staffList.StatusCode);
        await AssertApiErrorShapeAsync(staffList, 403);

        using var changedRole = await adminClient.PatchAsJsonAsync(
            $"/api/users/{staffId}/role", new { role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, changedRole.StatusCode);
        using var changedRoleBody = await JsonDocument.ParseAsync(await changedRole.Content.ReadAsStreamAsync());
        Assert.Contains("Admin", changedRoleBody.RootElement.GetProperty("roles").EnumerateArray()
            .Select(role => role.GetString()));

        using var revokedToken = await staffClient.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedToken.StatusCode);
        await AssertApiErrorShapeAsync(revokedToken, 401);

        using var refreshedLogin = await staffClient.PostAsJsonAsync(
            "/api/auth/login", new { email = staffEmail, password });
        Assert.Equal(HttpStatusCode.OK, refreshedLogin.StatusCode);
        using var refreshedBody = await JsonDocument.ParseAsync(await refreshedLogin.Content.ReadAsStreamAsync());
        SetToken(staffClient, refreshedBody.RootElement.GetProperty("token").GetString()!);
        using var selfRoleChange = await staffClient.PatchAsJsonAsync(
            $"/api/users/{staffId}/role", new { role = "Staff" });
        using var selfDeactivation = await staffClient.PatchAsJsonAsync(
            $"/api/users/{staffId}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, selfRoleChange.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, selfDeactivation.StatusCode);

        Assert.Equal($"http://localhost/api/users/{staffId}", created.Headers.Location?.ToString());
        SetToken(adminClient, adminToken);
    }

    [Fact]
    public async Task Deactivation_revokes_sessions_and_reactivation_clears_lockout()
    {
        using var host = new ApiTestHost();
        using var adminClient = host.CreateClient();
        await SetAdminTokenAsync(adminClient);

        const string staffEmail = "lockout-staff@integration.test";
        const string password = "LockoutStaff!2026";
        using var created = await adminClient.PostAsJsonAsync("/api/users", new
        {
            fullName = "Lockout Staff",
            email = staffEmail,
            password,
            role = "Staff"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdBody = await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync());
        var staffId = createdBody.RootElement.GetProperty("id").GetString()!;

        using var staffClient = host.CreateClient();
        using var login = await staffClient.PostAsJsonAsync("/api/auth/login", new { email = staffEmail, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginBody = await JsonDocument.ParseAsync(await login.Content.ReadAsStreamAsync());
        var staffToken = loginBody.RootElement.GetProperty("token").GetString()!;
        SetToken(staffClient, staffToken);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var failedLogin = await staffClient.PostAsJsonAsync(
                "/api/auth/login", new { email = staffEmail, password = "WrongPassword!2026" });
            Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);
        }

        using var lockedLogin = await staffClient.PostAsJsonAsync(
            "/api/auth/login", new { email = staffEmail, password = "WrongPassword!2026" });
        Assert.Equal(HttpStatusCode.Locked, lockedLogin.StatusCode);

        using var unlocked = await adminClient.PatchAsJsonAsync(
            $"/api/users/{staffId}/active", new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
        using var successfulLogin = await staffClient.PostAsJsonAsync(
            "/api/auth/login", new { email = staffEmail, password });
        Assert.Equal(HttpStatusCode.OK, successfulLogin.StatusCode);
        using var successfulLoginBody = await JsonDocument.ParseAsync(await successfulLogin.Content.ReadAsStreamAsync());
        var activeToken = successfulLoginBody.RootElement.GetProperty("token").GetString()!;

        using var deactivated = await adminClient.PatchAsJsonAsync(
            $"/api/users/{staffId}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        using var deactivatedBody = await JsonDocument.ParseAsync(await deactivated.Content.ReadAsStreamAsync());
        Assert.False(deactivatedBody.RootElement.GetProperty("isActive").GetBoolean());
        SetToken(staffClient, activeToken);
        using var revokedSession = await staffClient.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedSession.StatusCode);
        await AssertApiErrorShapeAsync(revokedSession, 401);

        using var rejectedLogin = await staffClient.PostAsJsonAsync(
            "/api/auth/login", new { email = staffEmail, password });
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedLogin.StatusCode);

        using var reactivated = await adminClient.PatchAsJsonAsync(
            $"/api/users/{staffId}/active", new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        using var reactivatedBody = await JsonDocument.ParseAsync(await reactivated.Content.ReadAsStreamAsync());
        Assert.True(reactivatedBody.RootElement.GetProperty("isActive").GetBoolean());
        using var reactivatedLogin = await staffClient.PostAsJsonAsync(
            "/api/auth/login", new { email = staffEmail, password });
        Assert.Equal(HttpStatusCode.OK, reactivatedLogin.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_unauthenticated_request()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertApiErrorShapeAsync(response, 401);
    }

    [Fact]
    public async Task Benchmark_query_diagnostics_are_disabled_by_default()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-EF-Query-Count"));
    }

    [Fact]
    public async Task Staff_cannot_mutate_products_or_categories()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);
        var categoryId = await CreateCategoryAsync(client);
        var productId = await CreateProductAsync(client, categoryId);
        var staffToken = await CreateStaffAsync(client);
        SetToken(client, staffToken);

        using var createProduct = await client.PostAsJsonAsync("/api/products", ProductPayload(categoryId));
        using var updateProduct = await client.PutAsJsonAsync($"/api/products/{productId}", new
        {
            name = "Changed product",
            description = "Updated",
            price = 12m,
            reorderLevel = 2,
            categoryId,
            isActive = true
        });
        using var deleteProduct = await client.DeleteAsync($"/api/products/{productId}");
        using var createCategory = await client.PostAsJsonAsync("/api/categories", new
        {
            name = $"Staff category {Guid.NewGuid():N}",
            description = "Not allowed"
        });
        using var deleteCategory = await client.DeleteAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, createProduct.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updateProduct.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteProduct.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteCategory.StatusCode);
        await AssertApiErrorShapeAsync(createProduct, 403);
    }

    [Fact]
    public async Task Anonymous_users_cannot_access_admin_only_endpoints()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();

        var responses = new[]
        {
            await client.GetAsync("/api/users"),
            await client.GetAsync("/api/users/integration-user-id"),
            await client.PostAsJsonAsync("/api/users", new
            {
                fullName = "Unauthorized User",
                email = "unauthorized@integration.test",
                password = "Unauthorized!2026",
                role = "Staff"
            }),
            await client.PatchAsJsonAsync("/api/users/integration-user-id/role", new { role = "Staff" }),
            await client.PatchAsJsonAsync("/api/users/integration-user-id/active", new { isActive = false }),
            await client.PostAsJsonAsync("/api/products", new { }),
            await client.PutAsJsonAsync("/api/products/1", new { }),
            await client.PatchAsJsonAsync("/api/products/1/stock", new { }),
            await client.DeleteAsync("/api/products/1"),
            await client.PostAsJsonAsync("/api/categories", new { }),
            await client.DeleteAsync("/api/categories/1")
        };

        using var responseCollection = new ResponseCollection(responses);
        Assert.All(responseCollection.Items, response =>
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));

        foreach (var response in responseCollection.Items)
            await AssertApiErrorShapeAsync(response, 401);
    }

    [Fact]
    public async Task Staff_can_create_customers_and_orders()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);
        var categoryId = await CreateCategoryAsync(client);
        var productId = await CreateProductAsync(client, categoryId, stock: 10);
        var staffToken = await CreateStaffAsync(client);
        SetToken(client, staffToken);

        using var customerResponse = await client.PostAsJsonAsync("/api/customers", CustomerPayload());
        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        using var customerBody = await JsonDocument.ParseAsync(await customerResponse.Content.ReadAsStreamAsync());
        var customerId = customerBody.RootElement.GetProperty("id").GetInt32();

        using var orderResponse = await client.PostAsJsonAsync("/api/orders", OrderPayload(customerId, productId, 1));
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
    }

    [Fact]
    public async Task Order_creation_rejects_non_positive_and_excessive_quantities()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);
        var categoryId = await CreateCategoryAsync(client);
        var productId = await CreateProductAsync(client, categoryId, stock: 10);
        var customerId = await CreateCustomerAsync(client);

        foreach (var quantity in new[] { 0, -1, 1_000_001 })
        {
            using var response = await client.PostAsJsonAsync(
                "/api/orders",
                OrderPayload(customerId, productId, quantity));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertApiErrorShapeAsync(response, 400);
        }

        Assert.Equal(10, await GetStockAsync(client, productId));
        using var orders = await client.GetAsync("/api/orders");
        using var ordersBody = await JsonDocument.ParseAsync(await orders.Content.ReadAsStreamAsync());
        Assert.Empty(ordersBody.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Order_status_transitions_reject_pending_and_disallowed_backward_transitions()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);
        var categoryId = await CreateCategoryAsync(client);
        var productId = await CreateProductAsync(client, categoryId, stock: 10);
        var customerId = await CreateCustomerAsync(client);

        async Task<int> CreateOrderAsync()
        {
            using var response = await client.PostAsJsonAsync(
                "/api/orders",
                OrderPayload(customerId, productId, 1));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            return body.RootElement.GetProperty("id").GetInt32();
        }

        async Task SetStatusAsync(int orderId, int status)
        {
            using var response = await client.PatchAsJsonAsync(
                $"/api/orders/{orderId}/status",
                new { status });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var pendingOrderId = await CreateOrderAsync();
        using (var pendingToPending = await client.PatchAsJsonAsync(
                   $"/api/orders/{pendingOrderId}/status",
                   new { status = 0 }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, pendingToPending.StatusCode);
            await AssertApiErrorShapeAsync(pendingToPending, 400);
        }

        var shippedOrderId = await CreateOrderAsync();
        await SetStatusAsync(shippedOrderId, 1);
        await SetStatusAsync(shippedOrderId, 2);
        using (var shippedToConfirmed = await client.PatchAsJsonAsync(
                   $"/api/orders/{shippedOrderId}/status",
                   new { status = 1 }))
        {
            Assert.Equal(HttpStatusCode.Conflict, shippedToConfirmed.StatusCode);
            await AssertApiErrorShapeAsync(shippedToConfirmed, 409);
        }

        var deliveredOrderId = await CreateOrderAsync();
        await SetStatusAsync(deliveredOrderId, 1);
        await SetStatusAsync(deliveredOrderId, 2);
        await SetStatusAsync(deliveredOrderId, 3);
        using (var deliveredToShipped = await client.PatchAsJsonAsync(
                   $"/api/orders/{deliveredOrderId}/status",
                   new { status = 2 }))
        {
            Assert.Equal(HttpStatusCode.Conflict, deliveredToShipped.StatusCode);
            await AssertApiErrorShapeAsync(deliveredToShipped, 409);
        }

        var cancelledOrderId = await CreateOrderAsync();
        await SetStatusAsync(cancelledOrderId, 4);
        using (var cancelledToConfirmed = await client.PatchAsJsonAsync(
                   $"/api/orders/{cancelledOrderId}/status",
                   new { status = 1 }))
        {
            Assert.Equal(HttpStatusCode.Conflict, cancelledToConfirmed.StatusCode);
            await AssertApiErrorShapeAsync(cancelledToConfirmed, 409);
        }
    }

    [Fact]
    public async Task Validation_and_model_binding_errors_use_the_same_shape()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);
        var categoryId = await CreateCategoryAsync(client);

        using var invalidDto = await client.PostAsJsonAsync("/api/products", new
        {
            name = "",
            sku = "",
            price = 0,
            stockQuantity = -1,
            reorderLevel = -1,
            categoryId
        });
        using var malformedJson = await client.PostAsync("/api/products",
            new StringContent("{\"name\":}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, invalidDto.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedJson.StatusCode);
        await AssertApiErrorShapeAsync(invalidDto, 400);
        await AssertApiErrorShapeAsync(malformedJson, 400);
    }

    [Fact]
    public async Task Registration_password_policy_errors_use_the_standard_validation_shape()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);

        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Weak Password User",
            email = "weak-password@integration.test",
            password = "weak"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertApiErrorShapeAsync(response, 400);
    }

    [Fact]
    public async Task Creating_and_cancelling_order_restores_stock_and_insufficient_stock_changes_nothing()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);
        var categoryId = await CreateCategoryAsync(client);
        var productId = await CreateProductAsync(client, categoryId, stock: 10);
        var customerId = await CreateCustomerAsync(client);

        using var order = await client.PostAsJsonAsync("/api/orders", OrderPayload(customerId, productId, 8));
        Assert.Equal(HttpStatusCode.Created, order.StatusCode);
        using var orderBody = await JsonDocument.ParseAsync(await order.Content.ReadAsStreamAsync());
        var orderId = orderBody.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(2, await GetStockAsync(client, productId));

        using var listResponse = await client.GetAsync("/api/orders?pageNumber=1&pageSize=10");
        using var orderList = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        var listedOrder = orderList.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == orderId);
        Assert.Equal("Integration Customer", listedOrder.GetProperty("customerName").GetString());
        var listedItem = Assert.Single(listedOrder.GetProperty("items").EnumerateArray());
        Assert.Equal(8, listedItem.GetProperty("quantity").GetInt32());
        Assert.Equal(80m, listedItem.GetProperty("lineTotal").GetDecimal());

        using var filteredOrders = await client.GetAsync("/api/orders?search=integration%20customer&status=0&sortBy=total&sortDescending=true");
        Assert.Equal(HttpStatusCode.OK, filteredOrders.StatusCode);
        using var filteredBody = await JsonDocument.ParseAsync(await filteredOrders.Content.ReadAsStreamAsync());
        Assert.Single(filteredBody.RootElement.GetProperty("items").EnumerateArray());

        using var invalidSort = await client.GetAsync("/api/orders?sortBy=unsupported");
        Assert.Equal(HttpStatusCode.BadRequest, invalidSort.StatusCode);
        await AssertApiErrorShapeAsync(invalidSort, 400);

        using var cancel = await client.PatchAsJsonAsync($"/api/orders/{orderId}/status", new { status = 4 });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(10, await GetStockAsync(client, productId));

        using var insufficient = await client.PostAsJsonAsync("/api/orders", OrderPayload(customerId, productId, 11));
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);
        await AssertApiErrorShapeAsync(insufficient, 409);
        Assert.Equal(10, await GetStockAsync(client, productId));
    }

    [Fact]
    public async Task Concurrent_orders_never_oversell_stock()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);
        var categoryId = await CreateCategoryAsync(client);
        var productId = await CreateProductAsync(client, categoryId, stock: 5);
        var customerId = await CreateCustomerAsync(client);

        var requests = Enumerable.Range(0, 20)
            .Select(_ => client.PostAsJsonAsync("/api/orders", OrderPayload(customerId, productId, 1)));
        using var responses = new ResponseCollection(await Task.WhenAll(requests));

        Assert.Equal(5, responses.Items.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(15, responses.Items.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.All(responses.Items, response =>
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
        Assert.Equal(0, await GetStockAsync(client, productId));
    }

    [Fact]
    public async Task Insights_reflect_sales_exclude_cancelled_orders_and_validate_parameters()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);
        var categoryId = await CreateCategoryAsync(client);
        var productId = await CreateProductAsync(client, categoryId, stock: 3, reorderLevel: 2);
        var customerId = await CreateCustomerAsync(client);

        using var completedSale = await client.PostAsJsonAsync("/api/orders", OrderPayload(customerId, productId, 2));
        Assert.Equal(HttpStatusCode.Created, completedSale.StatusCode);
        using var completedBody = await JsonDocument.ParseAsync(await completedSale.Content.ReadAsStreamAsync());
        using var cancelledSale = await client.PostAsJsonAsync("/api/orders", OrderPayload(customerId, productId, 1));
        using var cancelledBody = await JsonDocument.ParseAsync(await cancelledSale.Content.ReadAsStreamAsync());
        var cancelledId = cancelledBody.RootElement.GetProperty("id").GetInt32();
        using var cancellation = await client.PatchAsJsonAsync($"/api/orders/{cancelledId}/status", new { status = 4 });
        Assert.Equal(HttpStatusCode.OK, cancellation.StatusCode);

        using var summaryResponse = await client.GetAsync("/api/insights/summary");
        using var summary = await JsonDocument.ParseAsync(await summaryResponse.Content.ReadAsStreamAsync());
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        Assert.Equal(1, summary.RootElement.GetProperty("ordersInWindow").GetInt32());
        Assert.Equal(20m, summary.RootElement.GetProperty("revenueInWindow").GetDecimal());

        using var reorderResponse = await client.GetAsync("/api/insights/reorder-suggestions?limit=1");
        using var reorder = await JsonDocument.ParseAsync(await reorderResponse.Content.ReadAsStreamAsync());
        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);
        var suggestion = reorder.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("sku").GetString()!.StartsWith("TEST-", StringComparison.Ordinal));
        Assert.Equal(2, suggestion.GetProperty("unitsSold").GetInt32());
        Assert.True(suggestion.GetProperty("suggestedOrderQuantity").GetInt32() > 0);

        using var trendResponse = await client.GetAsync("/api/insights/sales-trend?days=1");
        using var trend = await JsonDocument.ParseAsync(await trendResponse.Content.ReadAsStreamAsync());
        Assert.Equal(HttpStatusCode.OK, trendResponse.StatusCode);
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var todayPoint = trend.RootElement.EnumerateArray()
            .Single(point => point.GetProperty("date").GetString() == today);
        Assert.Equal(1, todayPoint.GetProperty("orders").GetInt32());
        Assert.Equal(20m, todayPoint.GetProperty("revenue").GetDecimal());

        using var invalidLimit = await client.GetAsync("/api/insights/reorder-suggestions?limit=201");
        using var invalidDays = await client.GetAsync("/api/insights/sales-trend?days=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalidLimit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidDays.StatusCode);
        await AssertApiErrorShapeAsync(invalidLimit, 400);
        await AssertApiErrorShapeAsync(invalidDays, 400);
    }

    [Fact]
    public async Task Pagination_caps_page_size_and_returns_empty_items_for_out_of_range_page()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);

        using var cappedResponse = await client.GetAsync("/api/products?pageSize=500");
        using var capped = await JsonDocument.ParseAsync(await cappedResponse.Content.ReadAsStreamAsync());
        Assert.Equal(HttpStatusCode.OK, cappedResponse.StatusCode);
        Assert.Equal(50, capped.RootElement.GetProperty("pageSize").GetInt32());

        using var outOfRangeResponse = await client.GetAsync("/api/products?pageNumber=1000&pageSize=10");
        using var outOfRange = await JsonDocument.ParseAsync(await outOfRangeResponse.Content.ReadAsStreamAsync());
        Assert.Equal(HttpStatusCode.OK, outOfRangeResponse.StatusCode);
        Assert.Empty(outOfRange.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Low_stock_products_are_paged_with_a_hard_size_cap()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        await SetAdminTokenAsync(client);
        var categoryId = await CreateCategoryAsync(client);
        await CreateProductAsync(client, categoryId, stock: 0);

        using var cappedResponse = await client.GetAsync("/api/products/low-stock?pageSize=500");
        using var capped = await JsonDocument.ParseAsync(await cappedResponse.Content.ReadAsStreamAsync());
        Assert.Equal(HttpStatusCode.OK, cappedResponse.StatusCode);
        Assert.Equal(100, capped.RootElement.GetProperty("pageSize").GetInt32());
        Assert.All(capped.RootElement.GetProperty("items").EnumerateArray(), item =>
            Assert.True(item.GetProperty("stockQuantity").GetInt32() <= item.GetProperty("reorderLevel").GetInt32()));

        using var outOfRangeResponse = await client.GetAsync("/api/products/low-stock?pageNumber=1000&pageSize=10");
        using var outOfRange = await JsonDocument.ParseAsync(await outOfRangeResponse.Content.ReadAsStreamAsync());
        Assert.Equal(HttpStatusCode.OK, outOfRangeResponse.StatusCode);
        Assert.Empty(outOfRange.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Eleventh_auth_request_in_one_minute_is_rate_limited()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 11; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = "unknown@integration.test",
                password = "WrongPassword!2026"
            });
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(10), status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal((HttpStatusCode)429, statuses[10]);
        using var rejected = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "unknown@integration.test",
            password = "WrongPassword!2026"
        });
        await AssertApiErrorShapeAsync(rejected, 429);
    }

    [Fact]
    public async Task Cors_allows_the_configured_test_origin()
    {
        using var host = new ApiTestHost();
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        request.Headers.Add("Origin", ApiTestHost.CorsOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        Assert.Equal(ApiTestHost.CorsOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    private static async Task SetAdminTokenAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = ApiTestHost.AdminEmail,
            password = ApiTestHost.AdminPassword
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        SetToken(client, body.RootElement.GetProperty("token").GetString()!);
        using var protectedResponse = await client.GetAsync("/api/products");
        Assert.True(protectedResponse.IsSuccessStatusCode,
            $"Admin JWT rejected: {(int)protectedResponse.StatusCode}; {string.Join(", ", protectedResponse.Headers.WwwAuthenticate)}");
    }

    private static async Task<string> CreateStaffAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Staff User",
            email = $"staff-{Guid.NewGuid():N}@integration.test",
            password = "StaffAccount!2026"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("token").GetString()!;
    }

    private static void SetToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<int> CreateCategoryAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/categories", new
        {
            name = $"Test category {Guid.NewGuid():N}",
            description = "Integration test"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<int> CreateProductAsync(
        HttpClient client,
        int categoryId,
        int stock = 10,
        int reorderLevel = 2)
    {
        using var response = await client.PostAsJsonAsync("/api/products", ProductPayload(categoryId, stock, reorderLevel));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("id").GetInt32();
    }

    private static object ProductPayload(int categoryId, int stock = 10, int reorderLevel = 2) => new
    {
        name = $"Test product {Guid.NewGuid():N}",
        description = "Integration test",
        sku = $"TEST-{Guid.NewGuid():N}",
        price = 10m,
        stockQuantity = stock,
        reorderLevel,
        categoryId
    };

    private static object CustomerPayload() => new
    {
        fullName = "Integration Customer",
        email = $"customer-{Guid.NewGuid():N}@integration.test",
        phone = "555-0100",
        address = "Test address"
    };

    private static object OrderPayload(int customerId, int productId, int quantity) => new
    {
        customerId,
        items = new[] { new { productId, quantity } }
    };

    private static async Task<int> CreateCustomerAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/customers", CustomerPayload());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<int> GetStockAsync(HttpClient client, int productId)
    {
        using var response = await client.GetAsync($"/api/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("stockQuantity").GetInt32();
    }

    private static async Task AssertApiErrorShapeAsync(HttpResponseMessage response, int statusCode)
    {
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = body.RootElement;
        Assert.Equal(statusCode, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
        Assert.Equal(JsonValueKind.Object, root.GetProperty("errors").ValueKind);
    }

    private sealed class ResponseCollection : IDisposable
    {
        public IReadOnlyList<HttpResponseMessage> Items { get; }

        public ResponseCollection(IReadOnlyList<HttpResponseMessage> items) => Items = items;

        public void Dispose()
        {
            foreach (var response in Items)
                response.Dispose();
        }
    }
}
