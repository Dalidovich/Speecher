namespace Speecher.Hotkeys;

public sealed record HotkeyDefinition(uint Modifiers, VirtualKey Key, string DisplayName);
