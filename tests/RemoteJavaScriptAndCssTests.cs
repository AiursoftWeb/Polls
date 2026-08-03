using System.Text.RegularExpressions;

namespace Aiursoft.Polls.Tests;

[TestClass]
public class RemoteJavaScriptAndCssTests
{
    [TestMethod]
    public void CshtmlFiles_DoNotLoadRemoteJavaScriptOrCss()
    {
        var viewsPath = FindViewsPath();
        var remoteResources = Directory
            .EnumerateFiles(viewsPath, "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => FindRemoteResources(file, viewsPath))
            .ToList();

        Assert.AreEqual(
            0,
            remoteResources.Count,
            $"Remote JavaScript or CSS references were found:{Environment.NewLine}{string.Join(Environment.NewLine, remoteResources)}");
    }

    private static IEnumerable<string> FindRemoteResources(string file, string viewsPath)
    {
        var content = File.ReadAllText(file);

        foreach (Match tagMatch in Regex.Matches(content, @"<script\b[^>]*>|<link\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var tag = tagMatch.Value;
            var isScript = tag.StartsWith("<script", StringComparison.OrdinalIgnoreCase);
            var isStylesheet = !isScript && Regex.IsMatch(
                tag,
                @"\brel\s*=\s*(['""])[^'""]*\bstylesheet\b[^'""]*\1",
                RegexOptions.IgnoreCase);

            if (!isScript && !isStylesheet)
            {
                continue;
            }

            var resourceAttribute = isScript ? "src" : "href";
            var resourceMatch = Regex.Match(
                tag,
                $@"\b{resourceAttribute}\s*=\s*(['""])(?<url>(?:https?:)?//[^'""]+)\1",
                RegexOptions.IgnoreCase);

            if (resourceMatch.Success)
            {
                var relativePath = Path.GetRelativePath(viewsPath, file);
                yield return $"{relativePath}: {resourceMatch.Groups["url"].Value}";
            }
        }
    }

    private static string FindViewsPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Aiursoft.Polls", "Views");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find src/Aiursoft.Polls/Views.");
    }
}
