using Microsoft.AspNetCore.Http;
using Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;
using Lascodia.Trading.Engine.SharedApplication.Common.Models.AccessModel;
using Lascodia.Trading.Engine.SharedLibrary;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Services;

/// <summary>
/// Provides services to access the current user's information from the HTTP context.
/// </summary>
[ExcludeFromCodeCoverage]
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentUserService"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor to access the current HTTP context.</param>
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the current user's ID.
    /// </summary>
    public string UserId => User?.Id ?? string.Empty;

    /// <summary>
    /// Gets the current user's information.
    /// </summary>
    public UserVM? User => GetPassportUserByToken();

    public string? BearerToken { get; set; } = string.Empty;

    /// <summary>
    /// Retrieves the current user's information from the access token or JWT token.
    /// </summary>
    /// <returns>The current user's information as a <see cref="UserVM"/> object, or null if not authenticated.</returns>
    private UserVM? GetPassportUserByToken()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User?.Identity?.IsAuthenticated ?? false)
        {
            var headers = context.Request.Headers;
            ExtractBearerToken(headers);
            var decryptedToken = GetDecryptedToken(headers);
            if (!string.IsNullOrWhiteSpace(decryptedToken))
            {
                return GetUserFromDecryptedToken(headers, decryptedToken);
            }

            var userHeader = headers["user"].ToString();
            if (!string.IsNullOrWhiteSpace(userHeader))
            {
                return userHeader.JsonToObject<UserVM>();
            }

            return GetUserFromJwtToken(headers);
        }

        return null;
    }

    /// <summary>
    /// Extracts the bearer token from the request headers and assigns it to the BearerToken variable.
    /// </summary>
    /// <param name="headers">The request headers.</param>
    private void ExtractBearerToken(IHeaderDictionary headers)
    {
        var token = headers["Authorization"].FirstOrDefault()?.Split(" ").LastOrDefault();
        if (!string.IsNullOrWhiteSpace(token))
        {
            BearerToken = token;
        }
    }

    /// <summary>
    /// Retrieves the decrypted token from the request headers.
    /// </summary>
    /// <param name="headers">The request headers.</param>
    /// <returns>The decrypted token as a string.</returns>
    private static string GetDecryptedToken(IHeaderDictionary headers)
    {
        var decryptedToken = headers["decrytedAccessToken"].ToString();
        if (string.IsNullOrWhiteSpace(decryptedToken))
        {
            var accessToken = headers["accessToken"].ToString();
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                decryptedToken = Helper.Decrypt(accessToken, "qtc".To16BytesHash(), "qtc".To16BytesHash());
                headers["decrytedAccessToken"] = decryptedToken;
            }
        }
        return decryptedToken;
    }

    /// <summary>
    /// Extracts the user information from the decrypted token.
    /// </summary>
    /// <param name="headers">The request headers.</param>
    /// <param name="decryptedToken">The decrypted token.</param>
    /// <returns>The user information as a <see cref="UserVM"/> object, or null if not found.</returns>
    private static UserVM? GetUserFromDecryptedToken(IHeaderDictionary headers, string decryptedToken)
    {
        var json = decryptedToken.JsonToObject<AccessTokenVM>();
        if (json?.user != null)
        {
            headers["user"] = json.user.GetJson();
            return json.user;
        }
        return null;
    }

    /// <summary>
    /// Extracts the user information from the JWT token.
    /// </summary>
    /// <param name="headers">The request headers.</param>
    /// <returns>The user information as a <see cref="UserVM"/> object, or null if not found.</returns>
    private UserVM? GetUserFromJwtToken(IHeaderDictionary headers)
    {
        var token = headers["Authorization"].FirstOrDefault()?.Split(" ").LastOrDefault();
        if (!string.IsNullOrWhiteSpace(token))
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;

            if (jwtToken != null)
            {
                return new UserVM
                {
                    Id = jwtToken.Claims.First(x => x.Type == "passportId").Value,
                    FirstName = jwtToken.Claims.First(s => s.Type == "firstName").Value,
                    LastName = jwtToken.Claims.First(s => s.Type == "lastName").Value,
                    Email = jwtToken.Claims.First(s => s.Type == "email").Value,
                    PhoneNumber = jwtToken.Claims.First(s => s.Type == "mobileNo").Value
                };
            }
        }
        return null;
    }
}
