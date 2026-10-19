using Microsoft.AspNetCore.Http;
using Lascodia.Trading.Engine.SharedApplication.Common.Interfaces;
using Lascodia.Trading.Engine.SharedApplication.Common.Models.AccessModel;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Services;

/// <summary>
/// Resolves the current user from the request's authenticated principal.
/// </summary>
/// <remarks>
/// Identity comes only from the claims of the principal the authentication middleware
/// validated (signature, issuer, audience, lifetime). Request headers never contribute
/// identity: a header is whatever the caller chose to send, so reading a user, an access
/// token or a profile from one would let any caller act as anyone.
/// </remarks>
[ExcludeFromCodeCoverage]
public class CurrentUserService : ICurrentUserService
{
    /// <summary>Claim carrying the user's id. Without it the request has no user.</summary>
    public const string IdClaim = "passportId";

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
    /// Gets the current user's ID, or an empty string when the request is not authenticated.
    /// </summary>
    public string UserId => User?.Id ?? string.Empty;

    /// <summary>
    /// Gets the current user's information, or null when the request is not authenticated
    /// or its principal carries no <see cref="IdClaim"/>.
    /// </summary>
    public UserVM? User => FromPrincipal(_httpContextAccessor.HttpContext?.User);

    /// <summary>
    /// The bearer token presented in the <c>Authorization</c> header of an authenticated
    /// request. Informational only — it is never used to establish identity.
    /// </summary>
    public string? BearerToken
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.User?.Identity?.IsAuthenticated != true) return string.Empty;
            var header = context.Request.Headers.Authorization.ToString();
            const string scheme = "Bearer ";
            return header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
                ? header[scheme.Length..].Trim()
                : string.Empty;
        }
    }

    /// <summary>
    /// Builds the user from an authenticated principal's claims. Returns null for an
    /// unauthenticated principal or one without an id claim — the same answer as "nobody",
    /// rather than a blank identity that would sign audit rows as no one.
    /// </summary>
    public static UserVM? FromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true) return null;

        var id = Claim(principal, IdClaim);
        if (string.IsNullOrWhiteSpace(id)) return null;

        return new UserVM
        {
            Id = id,
            // Profile decoration — never gates anything, so an absent claim is an empty field.
            FirstName = Claim(principal, "firstName"),
            LastName = Claim(principal, "lastName"),
            Email = Claim(principal, "email"),
            PhoneNumber = Claim(principal, "mobileNo"),
        };
    }

    /// <summary>
    /// Value of the first claim of this JWT claim type, or empty. The JWT bearer handler maps
    /// some registered claim names to framework URIs on the way in (<c>email</c> becomes
    /// <see cref="ClaimTypes.Email"/>), so the mapped name is consulted too.
    /// </summary>
    private static string Claim(ClaimsPrincipal principal, string type)
    {
        var value = principal.FindFirst(type)?.Value;
        if (value is null && JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.TryGetValue(type, out var mapped))
            value = principal.FindFirst(mapped)?.Value;
        return value ?? string.Empty;
    }
}
