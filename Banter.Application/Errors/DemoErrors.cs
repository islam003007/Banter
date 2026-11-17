using Banter.SharedKernel;

namespace Banter.Application.Errors;

internal static class DemoErrors
{
    public static readonly Error Disabled =
        Error.NotFound("Demo.Disabled", "Demo mode is not enabled.");

    public static readonly Error UserNotAllowed =
        Error.NotFound("Demo.UserNotAllowed", "The requested user is not available for demo sign-in.");
}
