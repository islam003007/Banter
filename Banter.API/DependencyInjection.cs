using Banter.API.Constants;
using Banter.API.Endpoints;
using Banter.API.ExceptionHandlers;
using Banter.API.Options;
using Banter.Application.Constants;
using Banter.Application.Features.Auth;
using Banter.Domain;
using Banter.Domain.Users;
using Banter.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Serilog.Extensions.Hosting;
using System.Diagnostics;
using System.Threading.RateLimiting;

namespace Banter.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.Configure<DemoOptions>(configuration.GetSection(DemoOptions.SectionName));

        // Same-site frontend on a sibling subdomain. Origins come from the
        // environment only; no production origin is committed to the repository.
        // An empty list denies every cross-origin request (fail-closed).
        var allowedOrigins = configuration
            .GetSection("Frontend:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(Policies.FrontendCors, policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                return;
            }

            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }));

        var rateLimit = configuration
            .GetSection(RateLimitOptions.SectionName)
            .Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (!rateLimit.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimit.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimit.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        services.AddIdentity<User, Role>(options =>
        {
            options.User.RequireUniqueEmail = true;

            // Demo scope: there is no email sender, so confirmation and lockout stay off.
            // Both flags must be turned on before a production deployment that exposes
            // public registration.
            options.SignIn.RequireConfirmedEmail = false;

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.AllowedForNewUsers = false;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Banter.Auth";

            // Explicit, environment-aware transport settings. SameSite=Lax still sends the cookie
            // on top-level navigations and same-site fetches, which is what the SPA needs, while
            // blocking most cross-site form posts. Production is HTTPS-only; local HTTP
            // development cannot set Secure cookies.
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.IsEssential = true;

            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // Configure the existing event object instead of replacing it. Identity wires
            // OnValidatePrincipal (security-stamp validation) into these events and replacing the
            // object would silently disable that check.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                return context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = 401,
                    Title = "Unauthorized",
                    Detail = "You must be logged in to access this resource."
                });
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                return context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = 403,
                    Title = "Forbidden",
                    Detail = "You don't have permission to access this resource."
                });
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.AdminOnly, policy =>
                policy.RequireRole(Roles.Admin));
        });

        services.AddProblemDetails(configure =>
        {
            configure.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.TryAdd("RequestId", context.HttpContext.TraceIdentifier);
                context.ProblemDetails.Extensions.TryAdd("TraceId", Activity.Current?.TraceId);
            };
        });

        services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes.AssignableTo<IEndpoint>(), publicOnly: false)
            .AsImplementedInterfaces()
            .WithTransientLifetime());

        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
