using Microsoft.AspNetCore.Mvc.Filters;
using Lascodia.Trading.Engine.SharedLibrary;
using Lascodia.Trading.Engine.SharedApplication.Common.Models.AccessModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Filters;

public class GlobalAccessCheckAttribute : ActionFilterAttribute
{
    public string[]? Permissions { get; }
    public string[]? BusinessTypes { get; }

    public GlobalAccessCheckAttribute(string[]? permissions = null, string[]? businessTypes = null)
    {
        Permissions = permissions;
        BusinessTypes = businessTypes;
    }


    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var headers = context.HttpContext.Request.Headers;
        var decrypt = context.HttpContext.Request.Headers["decrytedAccessToken"].ToString();
        if (string.IsNullOrWhiteSpace(decrypt))
        {
            var accessToken = context.HttpContext.Request.Headers["accessToken"].ToString();
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                decrypt = Helper.Decrypt(accessToken, "qtc".To16BytesHash(), "qtc".To16BytesHash());
                context.HttpContext.Request.Headers.Add("decrytedAccessToken", decrypt);
            }
        }



        AccessTokenVM json = decrypt.JsonToObject<AccessTokenVM>();

        // Fetch user permissions using service if permissions are not already in token
        if (json != null && json.user != null && (json.permissions == null || json.permissions.Count == 0))
        {
            var permissionsService = context.HttpContext.RequestServices.GetService<IUserPermissionsService>();

            if (permissionsService != null && !string.IsNullOrWhiteSpace(json.user.Id))
            {
                try
                {
                    var permissions = await permissionsService.GetUserPermissionsAsync(json.user.Id);
                    if (permissions != null)
                    {
                        json.permissions = permissions;
                    }
                }
                catch
                {
                    // Log error if needed, continue with empty permissions
                }
            }
        }

        List<bool> isValids = new List<bool>();

        if (Permissions?.Length > 0)
        {
            bool isPermitted = false;
            Permissions.ToList().ForEach(s =>
            {
                if (json != null && json.permissions != null && json.permissions.Select(s => s.Name).Contains(s.Trim()))
                {
                    isPermitted = true;
                }
            });
            isValids.Add(isPermitted);
        }


        if (BusinessTypes != null && BusinessTypes?.Length > 0)
        {
            bool isbPermitted = false;
            if (json != null && BusinessTypes != null && BusinessTypes.Contains(json.user?.BusinessType))
            {
                isbPermitted = true;
            }
            isValids.Add(isbPermitted);
        }


        if (isValids.IsEmptyOrNull() || isValids.All(s => s == false))
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        else
        {
            if (json?.user != null)
            {
                context.HttpContext.Request.Headers["user"] = json?.user.GetJson();
            }
        }

        await next();
    }
}
