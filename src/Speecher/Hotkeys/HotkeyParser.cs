using Speecher.Interop;

namespace Speecher.Hotkeys;

public static class HotkeyParser
{
    private static readonly (string Name, uint Flag)[] ModifierOrder =
    [
        ("Ctrl", User32.ModControl),
        ("Alt", User32.ModAlt),
        ("Shift", User32.ModShift),
        ("Win", User32.ModWin)
    ];

    private static readonly Dictionary<string, uint> ModifierAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = User32.ModControl,
        ["Control"] = User32.ModControl,
        ["Alt"] = User32.ModAlt,
        ["Shift"] = User32.ModShift,
        ["Win"] = User32.ModWin
    };

    public static bool TryParse(string? text, out HotkeyDefinition hotkey)
    {
        hotkey = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        uint modifiers = 0;
        foreach (var part in parts[..^1])
        {
            if (!ModifierAliases.TryGetValue(part, out var flag) || (modifiers & flag) != 0)
            {
                return false;
            }

            modifiers |= flag;
        }

        if (!TryParseKey(parts[^1], out var key, out var keyName))
        {
            return false;
        }

        var names = ModifierOrder.Where(m => (modifiers & m.Flag) != 0).Select(m => m.Name).Append(keyName);
        hotkey = new HotkeyDefinition(modifiers, key, string.Join('+', names));
        return true;
    }

    private static bool TryParseKey(string token, out VirtualKey key, out string keyName)
    {
        var candidate = token.Length == 1 && char.IsAsciiDigit(token[0]) ? "D" + token : token;
        keyName = string.Empty;
        key = default;

        if (ModifierAliases.ContainsKey(candidate) || !candidate.All(char.IsAsciiLetterOrDigit) || char.IsAsciiDigit(candidate[0]))
        {
            return false;
        }

        if (!Enum.TryParse(candidate, ignoreCase: true, out key) || !Enum.IsDefined(key))
        {
            return false;
        }

        keyName = Enum.GetNames<VirtualKey>().First(name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase));
        return true;
    }
}
