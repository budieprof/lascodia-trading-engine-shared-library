using Microsoft.Extensions.Configuration;
using Lascodia.Trading.Engine.SharedApplication.Common.Models;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Configs;

public class NotificationConfig : ConfigurationOption<NotificationConfig>
{
    public string? Topic { get; set; }
    public string? BootstrapServers { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    public static NotificationConfig Bind(IConfiguration config)
    {
        return new NotificationConfig()
        {
            Topic = config.GetSection(nameof(NotificationConfig))["Topic"],
            BootstrapServers = config.GetSection(nameof(NotificationConfig))["BootstrapServers"],
            Username = config.GetSection(nameof(NotificationConfig))["Username"],
            Password = config.GetSection(nameof(NotificationConfig))["Password"],
        };
    }
}
