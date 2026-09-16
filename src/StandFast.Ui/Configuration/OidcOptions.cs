using System.ComponentModel.DataAnnotations;

namespace StandFast.Ui.Configuration;

/// <summary>
/// OpenID Connect settings for the identity provider, bound from the <see cref="SectionName"/> configuration section. The app speaks plain
/// OIDC, so any compliant provider works; StandFast is configured against Kinde.
/// </summary>
public sealed class OidcOptions
{
    public const string SectionName = "Oidc";

    /// <summary>
    /// Issuer base URL, for example <c>https://yourbusiness.kinde.com</c>. The handler reads the provider's discovery document from
    /// <c>{Authority}/.well-known/openid-configuration</c>, so no individual endpoint has to be configured.
    /// </summary>
    [Required]
    [Url]
    public string Authority { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Confidential client secret for the authorization code exchange. Comes from Key Vault in Azure and user secrets locally.</summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Redirect URI path the provider returns the authorization code to. Must be registered with the provider as an allowed callback URL.</summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>Path the provider returns to after ending its own session. Must be registered with the provider as an allowed logout redirect URL.</summary>
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>Where the user lands once sign-out has completed.</summary>
    public string SignedOutRedirectUri { get; set; } = "/";

    /// <summary>Scopes requested at sign-in. The defaults are what the board needs: an identifier, a display name, and an email address. Anything configured is added to these rather than replacing them.</summary>
    public IList<string> Scopes { get; set; } = ["openid", "profile", "email"];
}
