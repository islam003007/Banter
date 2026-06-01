using Banter.API.Constants;
using Banter.API.Endpoints;
using Banter.API.ExceptionHandlers;
using Banter.Application.Constants;
using Banter.Domain;
using Banter.Domain.Users;
using Banter.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Serilog.Extensions.Hosting;
using System.Diagnostics;

namespace Banter.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentity<User, Role>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false; // true for production

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;

            options.Lockout.AllowedForNewUsers = false; // true for production 
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Banter.Auth";

            options.Events = new Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents
            {
                OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    return context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = 401,
                        Title = "Unauthorized",
                        Detail = "You must be logged in to access this resource."
                    });
                },

                OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";

                    return context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = 403,
                        Title = "Forbidden",
                        Detail = "You don't have permission to access this resource."
                    });
                }
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
