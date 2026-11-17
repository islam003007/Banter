namespace Banter.Application.Features.Auth;

public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>Disabled by default; enabled only by the Development configuration.</summary>
    public bool Enabled { get; set; }
}
