using System;

namespace CoopServerConsole
{
    /// <summary>Small console helpers: colored output, headers, prompts.</summary>
    internal static class Ui
    {
        public static void Header(string title)
        {
            Console.Clear();
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================");
            Console.WriteLine("  " + title);
            Console.WriteLine("================================================================");
            Console.ForegroundColor = prev;
        }

        public static void Write(string text, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = prev;
        }

        public static void WriteLine(string text, ConsoleColor color)
        {
            Write(text + Environment.NewLine, color);
        }

        public static void Info(string t) { WriteLine(t, ConsoleColor.Gray); }
        public static void Good(string t) { WriteLine(t, ConsoleColor.Green); }
        public static void Warn(string t) { WriteLine(t, ConsoleColor.Yellow); }
        public static void Error(string t) { WriteLine(t, ConsoleColor.Red); }

        public static void Pause(string msg)
        {
            Console.WriteLine();
            WriteLine(msg, ConsoleColor.DarkGray);
            try { Console.ReadKey(true); } catch { }
        }

        public static void Pause() { Pause("Press any key to continue..."); }

        /// <summary>Line prompt that keeps the current value when the user just presses Enter.</summary>
        public static string Prompt(string label, string current)
        {
            Console.Write(label);
            if (!string.IsNullOrEmpty(current))
                Write(" [" + current + "]", ConsoleColor.DarkGray);
            Console.Write(": ");
            var input = Console.ReadLine();
            return string.IsNullOrEmpty(input) ? current : input.Trim();
        }

        public static bool Confirm(string label)
        {
            Console.Write(label + " (y/N): ");
            ConsoleKey k;
            try { k = Console.ReadKey(false).Key; } catch { return false; }
            Console.WriteLine();
            return k == ConsoleKey.Y;
        }

        public static bool KeyAvailable()
        {
            try { return Console.KeyAvailable; } catch { return false; }
        }
    }
}
