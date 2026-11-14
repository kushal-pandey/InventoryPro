using System.Text;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.RateLimiting;
using FluentValidation;
using InventoryPro.API;
using InventoryPro.API.Filters;
using InventoryPro.API.Middleware;
using InventoryPro.API.Models;
using InventoryPro.Application.Insights;
using InventoryPro.Application.Interfaces;
using InventoryPro.Application.Mappings;
using InventoryPro.Application.Services;
using InventoryPro.Application.Validators;
using InventoryPro.Domain.Entities;
using InventoryPro.Infrastructure.Data;
using InventoryPro.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
StartupConfigurationValidator.ValidateProductionGuards(builder.Configuration, builder.Environment);

var benchmarkMode = builder.Environment.IsDevelopment() &&
                    string.Equals(
                        Environment.GetEnvironmentVariable("INVENTORYPRO_BENCHMARK_SEED"),
                        "true",
                        StringComparison.OrdinalIgnoreCase);

// ---------- Database ----------
var provider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";

if (benchmarkMode)
    builder.Services.AddScoped<BenchmarkQueryCounter>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = builder.Configuration.GetConnectionString("Sqlite");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Sqlite must be configured.");

        options.UseSqlite(connectionString);
    }
    else if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = builder.Configuration.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:SqlServer must be configured.");

        options.UseSqlServer(connectionString);
    }
    else if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = builder.Configuration.GetConnectionString("PostgreSql");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:PostgreSql must be configured.");

        options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly("InventoryPro.PostgreSqlMigrations"));
    }
    else
    {
        throw new InvalidOperationException(
            $"Unsupported DatabaseProvider: '{provider}'. Use 'Sqlite', 'SqlServer' or 'PostgreSQL'.");
    }

    if (benchmarkMode)
        options.AddInterceptors(serviceProvider.GetRequiredService<BenchmarkQueryCounter>());
});

// ---------- Identity ----------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ---------- JWT Authentication ----------
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    Encoding.UTF8.GetByteCount(jwtKey) < 32 ||
    jwtKey.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
    jwtKey.Contains("change-me", StringComparison.OrdinalIgnoreCase) ||
    jwtKey.Contains("replace-this", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Configure Jwt:Key using User Secrets or an environment variable. It must be a non-placeholder secret of at least 32 UTF-8 bytes.");
}

if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");

if (!int.TryParse(jwtSection["ExpiryMinutes"], out var expiryMinutes) ||
    expiryMinutes is < 1 or > 1440)
{
    throw new InvalidOperationException("Jwt:ExpiryMinutes must be between 1 and 1440.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal;
            var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal?.FindFirstValue("sub");
            var tokenStamp = principal?.FindFirstValue("security_stamp");

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tokenStamp))
            {
                context.Fail("The token is no longer valid.");
                return;
            }

            var userManager = context.HttpContext.RequestServices
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            if (user is null || !user.IsActive ||
                !string.Equals(user.SecurityStamp, tokenStamp, StringComparison.Ordinal))
            {
                context.Fail("The token is no longer valid.");
            }
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(
                ApiErrorResponse.Create(
                    StatusCodes.Status401Unauthorized,
                    "Authentication is required."),
                context.HttpContext.RequestAborted);
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                ApiErrorResponse.Create(
                    StatusCodes.Status403Forbidden,
                    "You do not have permission to perform this action."),
                context.HttpContext.RequestAborted);
        }
    };
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

builder.Services.AddAuthorization();

// ---------- Application services ----------
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// ---------- Insights (reorder planner) ----------
var insightsSettings = builder.Configuration.GetSection("Insights").Get<InsightsSettings>() ?? new InsightsSettings();
insightsSettings.Validate();
builder.Services.AddSingleton(insightsSettings);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IInsightsService, InsightsService>();

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();

// ---------- Rate limiting ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiErrorResponse.Create(
                StatusCodes.Status429TooManyRequests,
                "Too many requests. Please wait a moment and try again."),
            cancellationToken);
    };
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// ---------- CORS ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

if (allowedOrigins.Length == 0 ||
    allowedOrigins.Any(origin => origin == "*" || !Uri.TryCreate(origin, UriKind.Absolute, out _)))
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins must contain explicit absolute origins and must not contain '*'.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "Authorization", "X-Correlation-ID"));
});

// ---------- Controllers + Swagger ----------
builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The supplied value is invalid."
                            : error.ErrorMessage)
                        .ToArray());

            return new BadRequestObjectResult(ApiErrorResponse.Create(
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                errors));
        };
    });
builder.Services.AddScoped<FluentValidationFilter>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "InventoryPro API",
        Version = "v1",
        Description = "Inventory & order management API with JWT authentication."
    });
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ---------- Database initialization ----------
// Production deployments should apply migrations as a deployment step. Startup
// migration is retained for Development and can be explicitly enabled if needed.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();

    if (app.Environment.IsDevelopment() ||
        builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        context.Database.Migrate();
    }

    if (await context.Database.CanConnectAsync())
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AppDbContextSeed");
        await AppDbContextSeed.SeedAsync(context, userManager, roleManager, app.Environment, logger);

        if (benchmarkMode)
            await BenchmarkDataSeeder.SeedAsync(context);
    }
    else if (app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("The configured database is not reachable.");
    }
}

// ---------- Middleware pipeline ----------
app.Use(async (context, next) =>
{
    var startedAt = Stopwatch.GetTimestamp();
    var suppliedId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    var correlationId = !string.IsNullOrWhiteSpace(suppliedId) &&
                        suppliedId.Length <= 64 &&
                        suppliedId.All(c => char.IsLetterOrDigit(c) || c is '-' or '_')
        ? suppliedId
        : Guid.NewGuid().ToString("N");

    context.TraceIdentifier = correlationId;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        return Task.CompletedTask;
    });

    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("RequestCorrelation");
    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        try
        {
            await next();
        }
        finally
        {
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (benchmarkMode)
{
    app.Use(async (context, next) =>
    {
        var queryCounter = context.RequestServices.GetRequiredService<BenchmarkQueryCounter>();
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-EF-Query-Count"] =
                queryCounter.CommandCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        });
        await next();
    });
}


if (app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "InventoryPro API v1"));
}


app.UseHttpsRedirection();
app.UseCors("AllowedOrigins");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }))
    .AllowAnonymous();

app.MapGet("/health/ready", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    try
    {
        return await db.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "Ready", database = "Reachable" })
            : Results.Json(ApiErrorResponse.Create(
                    StatusCodes.Status503ServiceUnavailable,
                    "The database is unreachable."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Database readiness check failed.");
        return Results.Json(ApiErrorResponse.Create(
                StatusCodes.Status503ServiceUnavailable,
                "The database readiness check failed."),
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.MapControllers();

app.Run();

/// <summary>Entry point for the InventoryPro API.</summary>
public partial class Program { }
