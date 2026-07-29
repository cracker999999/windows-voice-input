using System.Text;

namespace VoiceInput.Services;

internal static class TranscriptionText
{
    public static string Append(string existingText, string nextText, string language)
    {
        var builder = new StringBuilder(existingText.TrimEnd());
        AppendTo(builder, nextText, language);
        return builder.ToString();
    }

    public static string Join(IEnumerable<string> texts, string language)
    {
        var builder = new StringBuilder();
        foreach (var text in texts)
        {
            AppendTo(builder, text, language);
        }

        return builder.ToString();
    }

    public static void AppendTo(StringBuilder builder, string? nextText, string language)
    {
        var trimmedNextText = nextText?.Trim();
        if (string.IsNullOrEmpty(trimmedNextText))
        {
            return;
        }

        // 英文连续识别会按停顿切成多段，需要补空格；中日韩文本直接拼接，避免插入多余空格。
        var shouldSeparate = builder.Length > 0 &&
                             language.StartsWith("en", StringComparison.OrdinalIgnoreCase) &&
                             !char.IsWhiteSpace(builder[^1]) &&
                             !char.IsPunctuation(trimmedNextText[0]);
        if (shouldSeparate)
        {
            builder.Append(' ');
        }

        builder.Append(trimmedNextText);
    }
}
