namespace PiLoop.Services;

public sealed record PiPromptMetadata(string? Model = null);

public static class PiPromptMetadataReader
{
    public static async Task<PiPromptMetadata> ReadAsync(string promptPath)
    {
        if (!File.Exists(promptPath))
            return new PiPromptMetadata();

        var content = await File.ReadAllTextAsync(promptPath);
        return Parse(content);
    }

    internal static PiPromptMetadata Parse(string content)
    {
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return new PiPromptMetadata();

        var endIdx = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIdx < 0)
            return new PiPromptMetadata();

        var frontmatter = content[3..endIdx];
        string? model = null;

        foreach (var rawLine in frontmatter.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("model:", StringComparison.OrdinalIgnoreCase))
                model = CleanValue(line["model:".Length..]);
        }

        return new PiPromptMetadata(model);
    }

    private static string? CleanValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = value.Trim();
        if ((cleaned.StartsWith('"') && cleaned.EndsWith('"')) ||
            (cleaned.StartsWith('\'') && cleaned.EndsWith('\'')))
            cleaned = cleaned[1..^1];

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}
