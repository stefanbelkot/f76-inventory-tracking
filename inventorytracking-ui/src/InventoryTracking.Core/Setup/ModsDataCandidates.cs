using System.Text.RegularExpressions;

namespace InventoryTracking.Core.Setup;

/// <summary>A game install found automatically.</summary>
public sealed record GameCandidate(string Path, string Label, int ExportFiles);

/// <summary>Best-effort detection of Fallout 76 installs from a Steam root and a list of drive roots.</summary>
public static partial class ModsDataCandidates
{
    private static readonly string SteamRelative = Path.Combine("steamapps", "common", "Fallout76", "Data", "modsdata");
    private static readonly string XboxRelative = Path.Combine("XboxGames", "Fallout 76", "Content", "Data", "modsdata");

    public static IEnumerable<GameCandidate> Find(string? steamPath, IEnumerable<string> driveRoots)
    {
        var steam = SteamLibraries(steamPath).Select(library => (Path.Combine(library, SteamRelative), "Steam"));
        var xbox = driveRoots.Select(root => (Path.Combine(root, XboxRelative), "Xbox / Game Pass"));
        return steam.Concat(xbox)
            .Where(candidate => Directory.Exists(candidate.Item1))
            .DistinctBy(candidate => candidate.Item1, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => new GameCandidate(candidate.Item1, candidate.Item2, Sources.ExportFileName.CountIn(candidate.Item1)));
    }

    /// <summary>Extracts the library paths from the text of <c>libraryfolders.vdf</c>.</summary>
    public static IEnumerable<string> ParseLibraryFolders(string vdf) =>
        LibraryPath().Matches(vdf).Select(match => match.Groups[1].Value.Replace(@"\\", @"\"));

    private static IEnumerable<string> SteamLibraries(string? steamPath)
    {
        if (string.IsNullOrWhiteSpace(steamPath))
        {
            yield break;
        }

        yield return steamPath;
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
        {
            yield break;
        }

        string text;
        try
        {
            text = File.ReadAllText(vdf);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var library in ParseLibraryFolders(text))
        {
            yield return library;
        }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPath();
}
