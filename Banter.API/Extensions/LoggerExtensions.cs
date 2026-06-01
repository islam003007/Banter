using Serilog;
using System.Diagnostics;

namespace Banter.API.Extensions;

public static class LoggerExtensions
{
    public static IApplicationBuilder UseCustomRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
                diagnosticContext.Set("TraceId", Activity.Current?.TraceId);
            };
        });
    }
}
