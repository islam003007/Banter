using Banter.SharedKernel;

namespace Banter.Application.Errors;

internal class PageErrors
{
    public static Error InvalidCursor => Error.Problem("Pagination.InvalidCursor", "The page Cursor is invalid");
}
