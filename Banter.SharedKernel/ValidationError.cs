namespace Banter.SharedKernel;

public record ValidationError : MultiError
{
    public ValidationError(IEnumerable<Error> errors):
        base("Validation.General", "One or more validation errors occurred", errors)
    {

    }
}
