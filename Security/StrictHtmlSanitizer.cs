using Ganss.Xss;

namespace M03_Opus.Security;

/// <summary>
/// Làm sạch HTML với whitelist hẹp cho thông báo rich-text (Tình huống 11 &amp; 29).
/// </summary>
public sealed class StrictHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public StrictHtmlSanitizer()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "b", "strong", "i", "em", "u", "ul", "ol", "li", "a", "h3", "h4", "table", "thead", "tbody", "tr", "th", "td" })
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");

        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("https");
    }

    public string Sanitize(string? rawHtml)
    {
        if (string.IsNullOrWhiteSpace(rawHtml))
        {
            return string.Empty;
        }
        return _sanitizer.Sanitize(rawHtml);
    }
}
