namespace Lascodia.Trading.Engine.SharedApplication.Common.Models.AccessModel;

public class RoleVM
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }

    public int? BusinessId { get; set; }
}
