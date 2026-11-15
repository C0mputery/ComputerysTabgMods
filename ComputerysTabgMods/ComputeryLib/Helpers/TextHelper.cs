using System.Globalization;
using System.Text.RegularExpressions;

namespace ComputeryLib.Helpers;

public static class TextHelper {
    private static readonly Regex UnicodeEscapeRegex = new Regex(@"\\u([0-9A-Fa-f]{4})", RegexOptions.Compiled);
    
    public static string Reformat(string text) {
        string richText = text.Replace('[', '<').Replace(']', '>');
        return UnicodeEscapeRegex.Replace(richText, match => ((char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
    }
}