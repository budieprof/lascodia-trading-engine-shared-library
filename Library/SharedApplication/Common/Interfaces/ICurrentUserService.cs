using Lascodia.Trading.Engine.SharedApplication.Common.Models.AccessModel;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;

public interface ICurrentUserService
{
    string UserId { get;}
    UserVM? User { get; }
    string? BearerToken { get;}
}
