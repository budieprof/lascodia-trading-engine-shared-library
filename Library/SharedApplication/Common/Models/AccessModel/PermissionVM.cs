namespace Lascodia.Trading.Engine.SharedApplication.Common.Models.AccessModel;

public class PermissionVM
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }

    public int? ModuleId { get; set; }
}
