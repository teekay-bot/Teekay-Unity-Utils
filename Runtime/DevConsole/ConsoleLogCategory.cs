using UnityEngine;

namespace TeekayUtils.DevConsole
{
    /// <summary>
    /// Named log category with a color and an enable/disable flag (toggled at runtime via
    /// the log_filter command). Categories let projects partition their debug
    /// output (e.g., "Interaction", "Terrain", "AI") and color them distinctly.
    /// </summary>
    public sealed class ConsoleLogCategory
    {
        public string name;
        public Color color;
        public bool enabled = true;

        public ConsoleLogCategory(string name, Color color, bool enabled = true)
        {
            this.name = name;
            this.color = color;
            this.enabled = enabled;
        }
    }
}
