using System.ComponentModel.DataAnnotations;

namespace StandFast.Ui.Configuration;

/// <summary>How long a signed-in session lasts, bound from the <see cref="SectionName"/> configuration section.</summary>
public sealed class SignInSessionOptions
{
    public const string SectionName = "SignInSession";

    /// <summary>
    /// Days a session survives without the app being opened. Every visit restarts the count, and the session cookie outlives the browser, so someone who
    /// uses the app regularly stays signed in across restarts and weekends and signs in again only after this many idle days.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int IdleDays { get; set; } = 7;

    public TimeSpan IdleTimeout => TimeSpan.FromDays(IdleDays);
}
