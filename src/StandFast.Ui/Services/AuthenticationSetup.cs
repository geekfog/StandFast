using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using StandFast.Ui.Common;
using StandFast.Ui.Configuration;

namespace StandFast.Ui.Services;

/// <summary>
/// Wires standard OpenID Connect sign-in: an authorization code flow with PKCE against the configured provider, with the resulting session held
/// in a cookie. Nothing here is provider specific beyond configuration, so the identity provider can be changed without touching code.
/// </summary>
public static class AuthenticationSetup
{
    private const string AuthCookieName = "StandFast.Auth";

    /// <summary>Non-standard sign-out redirect parameter understood by Kinde. See the comment at its use site.</summary>
    private const string ProviderSignOutRedirectParameter = "redirect";

    public static void AddStandFastAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<OidcOptions>()
            .Bind(builder.Configuration.GetSection(OidcOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        OidcOptions oidc = builder.Configuration.GetSection(OidcOptions.SectionName).Get<OidcOptions>() ?? new OidcOptions();
        bool isDevelopment = builder.Environment.IsDevelopment();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.Name = AuthCookieName;
            options.Cookie.HttpOnly = true;

            // Lax is required rather than Strict: the provider redirects the browser back to the callback, and a Strict cookie is withheld on that cross-site navigation.
            options.Cookie.SameSite = SameSiteMode.Lax;

            // The http launch profile exists for local work; every deployed environment is HTTPS only.
            options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.SlidingExpiration = true;
        })
        .AddOpenIdConnect(options =>
        {
            // Endpoints are discovered from {Authority}/.well-known/openid-configuration, including the one used to end the provider's own session on sign-out.
            options.Authority = oidc.Authority;
            options.ClientId = oidc.ClientId;
            options.ClientSecret = oidc.ClientSecret;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.CallbackPath = oidc.CallbackPath;
            options.SignedOutCallbackPath = oidc.SignedOutCallbackPath;
            options.SignedOutRedirectUri = oidc.SignedOutRedirectUri;

            // The board only needs identity, never a downstream API call, so no tokens are kept after the code exchange.
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = true;

            // Keep claims under their OIDC names (sub, name, email) instead of remapping them to the legacy SOAP claim URIs.
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Name;

            // Kinde ends its session through a `redirect` query parameter rather than the RP-initiated `post_logout_redirect_uri` this handler sends,
            // and honours it without an id_token_hint (which is unavailable here because no tokens are kept). Sending both leaves a spec-compliant
            // provider unaffected, since an unrecognised parameter is ignored, while making Kinde return the user to the app after signing out.
            options.Events.OnRedirectToIdentityProviderForSignOut = context =>
            {
                string? postLogoutRedirectUri = context.ProtocolMessage.PostLogoutRedirectUri;
                if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri))
                {
                    context.ProtocolMessage.SetParameter(ProviderSignOutRedirectParameter, postLogoutRedirectUri);
                }

                return Task.CompletedTask;
            };

            options.Scope.Clear();
            foreach (string scope in oidc.Scopes)
            {
                options.Scope.Add(scope);
            }
        });

        builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddCascadingAuthenticationState();
    }

    /// <summary>
    /// Sign-in and sign-out endpoints. Both allow anonymous access: sign-in is the way in, and sign-out has to work even once the session cookie has expired,
    /// which the fallback authorisation policy would otherwise turn into a challenge.
    /// </summary>
    public static void MapStandFastAuthentication(this WebApplication app)
    {
        app.MapGet(UiRoutes.SignIn, ([FromQuery(Name = UiRoutes.ReturnUrlQueryKey)] string? returnUrl) =>
                TypedResults.Challenge(new AuthenticationProperties { RedirectUri = ToLocalUrl(returnUrl) }, [OpenIdConnectDefaults.AuthenticationScheme]))
            .AllowAnonymous();

        // Signing out of the cookie ends the local session; signing out of the OpenID Connect scheme ends the provider's session too, so the next
        // sign-in is a real one rather than a silent re-issue.
        app.MapGet(UiRoutes.SignOut, () =>
                TypedResults.SignOut(authenticationSchemes: [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
            .AllowAnonymous();
    }

    /// <summary>Accepts only site-relative return URLs, so a crafted sign-in link cannot bounce the user to another host after authenticating.</summary>
    private static string ToLocalUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : UiRoutes.Board;
}
