using System.Diagnostics.CodeAnalysis;
using FluentValidation.Results;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Exceptions;

[ExcludeFromCodeCoverage]
public class ValidationException : Exception
{
    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this(string.Join(",", failures.Select(s => s.ErrorMessage)))
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());
    }

    public IDictionary<string, string[]> Errors { get; }
}
