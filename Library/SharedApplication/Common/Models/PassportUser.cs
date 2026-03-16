namespace Lascodia.Trading.Engine.SharedApplication.Common.Models;

public class PassportUser
{
    public string? access_token { get; set; }
    public string? token_type { get; set; }
    public string? refresh_token { get; set; }
    public string? firstName { get; set; }
    public string? lastName { get; set; }
    public string? email { get; set; }
    public string? client_name { get; set; }
    public string? passportId { get; set; }
    public string? code { get; set; }

}
