using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Exceptions;

[ExcludeFromCodeCoverage]
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() : base() { }
}
