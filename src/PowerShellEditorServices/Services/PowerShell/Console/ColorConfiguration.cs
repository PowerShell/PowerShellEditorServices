using System;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    internal class ColorConfiguration
    {
        public ConsoleColor ForegroundColor { get; set; }

        public ConsoleColor BackgroundColor { get; set; }

        public ConsoleColor FormatAccentColor { get; set; }

        public ConsoleColor ErrorAccentColor { get; set; }

        public ConsoleColor ErrorForegroundColor { get; set; }

        public ConsoleColor ErrorBackgroundColor { get; set; }

        public ConsoleColor WarningForegroundColor { get; set; }

        public ConsoleColor WarningBackgroundColor { get; set; }

        public ConsoleColor DebugForegroundColor { get; set; }

        public ConsoleColor DebugBackgroundColor { get; set; }

        public ConsoleColor VerboseForegroundColor { get; set; }

        public ConsoleColor VerboseBackgroundColor { get; set; }

        public ConsoleColor ProgressForegroundColor { get; set; }

        public ConsoleColor ProgressBackgroundColor { get; set; }
    }
}
