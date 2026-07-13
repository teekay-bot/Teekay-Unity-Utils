using System;

namespace TeekayUtils
{
    public static class StringExtensions
    {
        /// <summary>Fluent alias for string.IsNullOrEmpty.</summary>
        public static bool IsNullOrEmpty(this string value) => string.IsNullOrEmpty(value);

        /// <summary>Fluent alias for string.IsNullOrWhiteSpace.</summary>
        public static bool IsNullOrWhiteSpace(this string value) => string.IsNullOrWhiteSpace(value);

        /// <summary>Returns the string, or an empty string if it is null.</summary>
        public static string OrEmpty(this string value) => value ?? string.Empty;

        /// <summary>Truncates to maxLength characters; shorter strings are returned unchanged.</summary>
        public static string Shorten(this string value, int maxLength)
        {
            if (value.IsNullOrWhiteSpace()) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        /// <summary>
        /// Slices from startIndex (inclusive) to endIndex (exclusive).
        /// A negative endIndex counts from the end of the string, Python-style.
        /// </summary>
        public static string Slice(this string value, int startIndex, int endIndex)
        {
            if (value.IsNullOrWhiteSpace())
            {
                throw new ArgumentNullException(nameof(value), "Value cannot be null or empty.");
            }

            if (startIndex < 0 || startIndex > value.Length - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            endIndex = endIndex < 0 ? value.Length + endIndex : endIndex;

            if (endIndex < 0 || endIndex < startIndex || endIndex > value.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            }

            return value.Substring(startIndex, endIndex - startIndex);
        }

        // Rich text formatting for TMP / UI elements that support rich text.
        public static string RichColor(this string text, string color) => $"<color={color}>{text}</color>";
        public static string RichSize(this string text, int size) => $"<size={size}>{text}</size>";
        public static string RichBold(this string text) => $"<b>{text}</b>";
        public static string RichItalic(this string text) => $"<i>{text}</i>";
        public static string RichUnderline(this string text) => $"<u>{text}</u>";
        public static string RichStrikethrough(this string text) => $"<s>{text}</s>";
        public static string RichFont(this string text, string font) => $"<font={font}>{text}</font>";
        public static string RichAlign(this string text, string align) => $"<align={align}>{text}</align>";
        public static string RichGradient(this string text, string color1, string color2) => $"<gradient={color1},{color2}>{text}</gradient>";
        public static string RichRotation(this string text, float angle) => $"<rotate={angle}>{text}</rotate>";
        public static string RichSpace(this string text, float space) => $"<space={space}>{text}</space>";
    }
}
