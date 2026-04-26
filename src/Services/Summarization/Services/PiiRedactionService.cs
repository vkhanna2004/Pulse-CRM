using System.Text.RegularExpressions;

namespace PulseCRM.Summarization.Services;

public static class PiiRedactionService
{
    private static readonly Regex EmailRegex = new(@"[\w.+]+@[\w.]+\.\w+", RegexOptions.Compiled);
    private static readonly Regex SsnRegex   = new(@"\d{3}-\d{2}-\d{4}", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"\+?1?[\s-]?\(?\d{3}\)?[\s-]?\d{3}[\s-]?\d{4}", RegexOptions.Compiled);

    public static string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? "";
        input = EmailRegex.Replace(input, "[EMAIL]");
        input = SsnRegex.Replace(input, "[SSN]");
        input = PhoneRegex.Replace(input, "[PHONE]");
        return input;
    }
}
