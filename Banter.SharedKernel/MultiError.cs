using System.Collections.ObjectModel;

namespace Banter.SharedKernel;

public record MultiError : Error
{
    public ReadOnlyCollection<Error> Errors { get; }

    public MultiError(IEnumerable<Error> errors) : this("General.MultibleErrors", "One or more errors occurred", errors)
    {

    }
    protected MultiError(string code, string description, IEnumerable<Error> errors) :
        base(code, description, ErrorType.MultiError)
    {
        Errors = errors.ToList().AsReadOnly();
    }
}
