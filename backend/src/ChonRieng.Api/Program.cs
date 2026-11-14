using System.Security.Claims;
using System.Text;
using ChonRieng.Api.Endpoints;
using ChonRieng.Api.Features;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Health;
using ChonRieng.Api.Authorization;
using ChonRieng.Api.Middleware;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
var seedDevelopmentData = DevelopmentSeedCommand.IsRequested(args);
if (seedDevelopmentData)
{
    DevelopmentSeedCommand.EnsureAllowed(builder.Environment);
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Set ConnectionStrings__DefaultConnection to a SQL Server connection string before starting the API.");
}

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException("Set Jwt__SigningKey to a secret containing at least 32 UTF-8 bytes.");
}

if (jwtOptions.AccessTokenMinutes <= 0 || jwtOptions.RefreshTokenDays <= 0)
{
    throw new InvalidOperationException("JWT access-token minutes and refresh-token days must be positive.");
}

var paymentOptions = builder.Configuration.GetSection(PaymentOptions.SectionName).Get<PaymentOptions>() ?? new PaymentOptions();
if (paymentOptions.ValidateOnStart)
{
    paymentOptions.ValidateOrThrow();
}

builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

builder.Services
    .AddIdentityCore<User>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped(typeof(IEntityCrudService<>), typeof(EfEntityCrudService<>));
builder.Services.AddScoped<IBranchAccessService, BranchAccessService>();
builder.Services.AddScoped<IAuthorizationHandler, BranchScopeAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, CustomerOwnResourceAuthorizationHandler>();
builder.Services.AddFeatureServices();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BranchScope", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(BusinessRoles.Admin, BusinessRoles.BranchManager, BusinessRoles.Staff);
        policy.AddRequirements(new BranchScopeRequirement());
    });
    options.AddPolicy("CustomerOwnResource", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new CustomerOwnResourceRequirement());
    });
    options.AddFeaturePolicies();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Chốn Riêng API",
        Version = "v1",
        Description = "Tài liệu API cho hệ thống quản lý chuỗi cà phê Chốn Riêng. Admin có quyền toàn hệ thống; BranchManager và Staff chỉ được truy cập chi nhánh có trong token; Customer chỉ được truy cập tài nguyên của chính mình. CRUD tổng quát hiện vẫn chỉ dành cho Admin.",
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập access token JWT. Có thể lấy token bằng API đăng nhập.",
    });
    options.SchemaFilter<EnumAsStringSchemaFilter>();
    // Feature DTOs may share a name with admin CRUD DTOs (e.g. ZoneResponse); prefix them with their area.
    options.CustomSchemaIds(SwaggerSchemaIds.For);
    options.OperationFilter<AuthorizeOperationFilter>();
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("RoleBootstrapper");
    await RoleBootstrapper.InitializeAsync(scope.ServiceProvider, app.Configuration, logger);
    await DatabaseHealth.LogStartupStatusAsync(
        scope.ServiceProvider,
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseHealth"),
        app.Lifetime.ApplicationStopping);

    if (seedDevelopmentData)
    {
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DevelopmentDataSeeder");
        await DevelopmentDataSeeder.SeedAsync(
            scope.ServiceProvider,
            app.Configuration,
            seedLogger,
            app.Lifetime.ApplicationStopping);
    }
}

if (seedDevelopmentData)
{
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapAuthEndpoints();
app.MapAdminCrudEndpoints();
app.MapFeatureEndpoints();
app.MapHealthEndpoints();

app.Run();

public partial class Program;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any())
        {
            return;
        }

        var authorizationDescription = metadata.OfType<AuthorizationDescriptionMetadata>().LastOrDefault();
        if (authorizationDescription is not null)
        {
            operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                ? authorizationDescription.Description
                : $"{operation.Description}\n\n**Phân quyền:** {authorizationDescription.Description}";
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            }] = Array.Empty<string>(),
        });
        operation.Responses.TryAdd(
            StatusCodes.Status401Unauthorized.ToString(),
            new OpenApiResponse { Description = "Chưa đăng nhập hoặc access token không hợp lệ/hết hạn." });
        operation.Responses.TryAdd(
            StatusCodes.Status403Forbidden.ToString(),
            new OpenApiResponse { Description = "Không có quyền truy cập theo vai trò hoặc phạm vi chi nhánh." });
    }
}

public static class SwaggerSchemaIds
{
    private const string FeatureNamespace = "ChonRieng.Api.Features.";

    public static string For(Type type)
    {
        var name = type.IsGenericType
            ? string.Concat(type.GetGenericArguments().Select(For)) + type.Name[..type.Name.IndexOf('`')]
            : type.Name;
        return type.Namespace?.StartsWith(FeatureNamespace, StringComparison.Ordinal) == true
            ? type.Namespace[FeatureNamespace.Length..].Replace(".", "") + name
            : name;
    }
}

public sealed class EnumAsStringSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var enumType = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
        if (!enumType.IsEnum)
        {
            return;
        }

        schema.Type = "string";
        schema.Format = null;
        schema.Enum = Enum.GetNames(enumType)
            .Select(name => (IOpenApiAny)new OpenApiString(name))
            .ToList();
    }
}
