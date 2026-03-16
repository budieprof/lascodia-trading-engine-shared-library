namespace Lascodia.Trading.Engine.SharedApplication.Common.Models.AccessModel;

public class UserVM
{
    public string? Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MiddleName { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int? BusinessId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? UserType { get; set; }
    public string? AdminType { get; set; }
    public string? BusinessType { get; set; }

    public string? PhotoUrl { get; set; }
}
