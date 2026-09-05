namespace Mosaic.Desktop.Tests;

// Walks up from the test output to the folder containing Mosaic.slnx. The drift tests need the source tree.
internal static class RepoLayout
{
    public static string Source { get; } = Path.Combine(Root(), "src");

    public static string Desktop { get; } = Path.Combine(Source, "Mosaic.Desktop");

    public static string Themes { get; } = Path.Combine(Desktop, "Themes");

    private static string Root()
    {
        var searched = new List<string>();

        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            searched.Add(folder.FullName);

            if (File.Exists(Path.Combine(folder.FullName, "Mosaic.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException(
            $"No Mosaic.slnx above the test output, so the source tree cannot be read. " +
            $"Looked in: {string.Join(", ", searched)}");
    }
}
