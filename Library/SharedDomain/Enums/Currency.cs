using System.ComponentModel;

namespace Lascodia.Trading.Engine.SharedDomain.Enums;

public enum Currency
{
    [Description("NAIRA")]
    NGN = 0,
    [Description("DOLLAR")]
    USD = 1,
    [Description("POUND")]
    GBP = 2,
    [Description("EURO")]
    EUR = 3,
}
