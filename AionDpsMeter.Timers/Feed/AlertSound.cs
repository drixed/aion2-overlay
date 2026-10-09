namespace AionDpsMeter.Timers.Feed;

/// <summary>The sounds Windows has built in (System.Media.SystemSounds).</summary>
public enum WindowsSound { Exclamation, Asterisk, Beep, Hand, Question }

/// <summary>
/// What an alert plays: the standard sound, a Windows sound or the user's own file (.wav/.mp3). Saved as text:
/// "" (standard), "system:asterisk", "file:C:\…\shugo.mp3".
/// </summary>
public sealed record AlertSound
{
    private const string SystemPrefix = "system:";
    private const string FilePrefix = "file:";

    public static readonly AlertSound Default = new();

    public WindowsSound? Windows { get; private init; }
    public string? File { get; private init; }

    public bool IsDefault => Windows is null && File is null;

    public static AlertSound System(WindowsSound sound) => new() { Windows = sound };

    public static AlertSound FromFile(string path) =>
        string.IsNullOrWhiteSpace(path) ? Default : new() { File = path };

    public static AlertSound Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Default;
        if (text.StartsWith(SystemPrefix, StringComparison.OrdinalIgnoreCase))
            return Enum.TryParse<WindowsSound>(text[SystemPrefix.Length..], ignoreCase: true, out var sound) ? System(sound) : Default;
        if (text.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase))
            return FromFile(text[FilePrefix.Length..]);
        return Default;
    }

    public override string ToString() =>
        Windows is { } w ? SystemPrefix + w.ToString().ToLowerInvariant()
        : File is { } f ? FilePrefix + f
        : "";
}
