using System;
using UnityEngine;

namespace TeekayUtils
{
    public static class ColorExtensions
    {
        /// <summary>Returns a copy with the given alpha.</summary>
        public static Color SetAlpha(this Color color, float alpha)
            => new(color.r, color.g, color.b, alpha);

        /// <summary>Adds two colors component-wise, clamped to 0-1.</summary>
        public static Color Add(this Color thisColor, Color otherColor)
            => (thisColor + otherColor).Clamp01();

        /// <summary>Subtracts a color component-wise, clamped to 0-1.</summary>
        public static Color Subtract(this Color thisColor, Color otherColor)
            => (thisColor - otherColor).Clamp01();

        /// <summary>Inverts RGB, keeping alpha.</summary>
        public static Color Invert(this Color color)
            => new(1 - color.r, 1 - color.g, 1 - color.b, color.a);

        /// <summary>Converts to a "#RRGGBBAA" hex string.</summary>
        public static string ToHex(this Color color)
            => $"#{ColorUtility.ToHtmlStringRGBA(color)}";

        /// <summary>
        /// Parses a hex/HTML color string ("#RRGGBB", "#RRGGBBAA" or names like "red").
        /// Plain static method rather than a string extension to keep string
        /// IntelliSense clean.
        /// </summary>
        public static Color FromHex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }

            throw new ArgumentException($"Invalid hex color string '{hex}'", nameof(hex));
        }

        static Color Clamp01(this Color color)
        {
            return new Color
            {
                r = Mathf.Clamp01(color.r),
                g = Mathf.Clamp01(color.g),
                b = Mathf.Clamp01(color.b),
                a = Mathf.Clamp01(color.a)
            };
        }
    }
}
