using Microsoft.Extensions.Configuration;
using Lascodia.Trading.Engine.SharedApplication.Common.Models;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Configs;

public class UserPermissionsConfig : ConfigurationOption<UserPermissionsConfig>
{
    public string? FetchUserPermissionsUrl { get; set; }

    public static UserPermissionsConfig Bind(IConfiguration config)
    {
        return new UserPermissionsConfig()
        {
            FetchUserPermissionsUrl = config.GetSection(nameof(UserPermissionsConfig))["FetchUserPermissionsUrl"],
        };
    }
}
