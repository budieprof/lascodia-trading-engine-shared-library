using Lascodia.Trading.Engine.SharedApplication.Common.Models.AccessModel;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;

public interface IUserPermissionsService
{
    Task<List<PermissionVM>?> GetUserPermissionsAsync(string userId);
}
