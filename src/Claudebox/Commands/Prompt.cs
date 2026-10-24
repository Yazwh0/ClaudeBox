namespace Claudebox.Commands;

internal static class Prompt
{
    /// <summary>Shows the options and waits for one of their keys (case-insensitive).</summary>
    public static char Choose(params (char Key, string Label)[] options)
    {
        foreach (var (key, label) in options)
            Console.WriteLine($"  [{key}] {label}");

        while (true)
        {
            var key = char.ToUpperInvariant(Console.ReadKey(intercept: true).KeyChar);
            if (options.Any(o => o.Key == key))
            {
                Console.WriteLine();
                return key;
            }
        }
    }

    public static string Ask(string question, string defaultAnswer)
    {
        Console.Write($"{question} [{defaultAnswer}]: ");
        var answer = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(answer) ? defaultAnswer : answer;
    }
}
