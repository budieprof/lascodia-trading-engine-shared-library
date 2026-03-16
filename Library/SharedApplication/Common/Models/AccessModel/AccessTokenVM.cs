namespace Lascodia.Trading.Engine.SharedApplication.Common.Models.AccessModel;

public class AccessTokenVM
{
	public UserVM? user { get; set; }
	public List<PermissionVM>? permissions { get; set; }
}
