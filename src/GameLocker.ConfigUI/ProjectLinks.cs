using System.Reflection;

namespace GameLocker.ConfigUI;

internal sealed record ProjectLink(string Label, Uri? Uri, Image Logo);

internal static class ProjectLinks
{
    public static IReadOnlyList<ProjectLink> All { get; } =
    [
        Read("GitHub", "GameLockerGitHubUrl", $"github-{MarkColor}.png"),
        Read("Website", "GameLockerWebsiteUrl", "website.png"),
        Read("LinkedIn", "GameLockerLinkedInUrl", $"linkedin-{MarkColor}.png"),
    ];

    private static string MarkColor => WorkspaceTheme.Raised.GetBrightness() > 0.5F ? "black" : "white";

    private static ProjectLink Read(string label, string key, string fileName)
    {
        var assembly = typeof(ProjectLinks).Assembly;
        var value = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)?.Value;
        var valid = Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 && uri.UserInfo.Length == 0;

        using var stream = assembly.GetManifestResourceStream($"GameLocker.ConfigUI.Assets.{fileName}")
            ?? throw new InvalidOperationException($"Missing project logo {fileName}.");
        using var artwork = Image.FromStream(stream);
        return new ProjectLink(label, valid ? uri : null, new Bitmap(artwork));
    }
}
