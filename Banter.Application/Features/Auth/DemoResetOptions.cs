using System.ComponentModel.DataAnnotations;

namespace Banter.Application.Features.Auth;

public sealed class DemoResetOptions
{
    public const string SectionName = "Demo:Reset";

    public bool Enabled { get; set; }

    [Range(1, int.MaxValue)]
    public int IntervalHours { get; set; } = 24;
}
