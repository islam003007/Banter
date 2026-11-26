using Banter.Application.Abstractions.Behaviors;
using Banter.Application.Abstractions.Bots;
using Banter.Application.Bots;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Banter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        // Register every AbstractValidator in this assembly. Without this the validation
        // pipeline behaviour only ever saw an empty IEnumerable<IValidator<T>>, so request
        // validation silently did nothing.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        if (configuration.GetValue<bool>($"{BotOptions.SectionName}:Enabled"))
        {
            services.AddScoped<IBotReplyProcessor, BotReplyProcessor>();
        }

        return services;
    }
}
