using System.Reflection;

namespace GameLocker.ConfigUI;

internal sealed record ProjectLink(string Label, Uri? Uri, Image LightLogo, Image DarkLogo)
{
    public Image Logo(WorkspaceTheme theme) => theme.UseLightLogos ? LightLogo : DarkLogo;
}

internal static class ProjectLinks
{
    public static IReadOnlyList<ProjectLink> All { get; } =
    [
        Read("GitHub", "GameLockerGitHubUrl", "github-white.png", "github-black.png"),
        Read("Website", "GameLockerWebsiteUrl", "website.png"),
        Read("LinkedIn", "GameLockerLinkedInUrl", "linkedin-white.png", "linkedin-black.png"),
    ];

    private static ProjectLink Read(string label, string key, string lightMark, string? darkMark = null)
    {
        var assembly = typeof(ProjectLinks).Assembly;
        var value = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)?.Value;
        var valid = Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 && uri.UserInfo.Length == 0;

        var lightLogo = ReadLogo(lightMark);
        return new ProjectLink(label, valid ? uri : null, lightLogo, darkMark is null ? lightLogo : ReadLogo(darkMark));

        Image ReadLogo(string fileName)
        {
            using var stream = assembly.GetManifestResourceStream($"GameLocker.ConfigUI.Assets.{fileName}")
                ?? throw new InvalidOperationException($"Missing project logo {fileName}.");
            using var artwork = Image.FromStream(stream);
            return new Bitmap(artwork);
        }
    }
}
