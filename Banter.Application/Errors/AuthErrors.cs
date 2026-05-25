using Banter.SharedKernel;

namespace Banter.Application.Errors;

internal static class AuthErrors
{
    public static Error LockedOut = Error.Problem("Auth.LockedOut",
        "You were locked out for trying to log in too many times, try again after a few minutes");
    public static Error EmailNotConfirmed => Error.Problem("Auth.EmailNotConfirmed", "Please Confirm your email before loging in");
    public static Error LoginFailed = Error.Problem("Auth.loginFailed", "Login Failed");
}
