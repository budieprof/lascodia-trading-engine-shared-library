namespace Lascodia.Trading.Engine.SharedApplication.Common.Models.AccessModel;

public class UserPermissionData
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? AppModule { get; set; }
    public List<string>? BusinessTypes { get; set; }
}

public class UserPermissionResponse
{
    public List<UserPermissionData>? Data { get; set; }
    public bool Status { get; set; }
    public string? Message { get; set; }
    public string? ResponseCode { get; set; }
}
