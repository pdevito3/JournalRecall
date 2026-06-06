using Ardalis.SmartEnum;

namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// How the user felt during a Session (CONTEXT.md, PRD-0006): one of the app-defined known moods, or a
/// Custom mood that carries the user's own words. A value object built on a private <see cref="MoodType"/>
/// SmartEnum — the outside world deals in <see cref="Mood"/> and its canonical string <see cref="Value"/>
/// (a known mood name, or the custom text). A Session can carry several Moods, persisted as a string[]
/// (JSON column); the literal "Custom" sentinel is never persisted.
/// </summary>
public sealed record Mood
{
    private readonly MoodType _type;

    /// <summary>The user's free text for a Custom mood; null for a known mood.</summary>
    public string? CustomValue { get; }

    private Mood(MoodType type, string? customValue)
    {
        _type = type;
        CustomValue = customValue;
    }

    /// <summary>True when this is the free-text Custom mood.</summary>
    public bool IsCustom => _type == MoodType.Custom;

    /// <summary>
    /// The canonical persisted string: the known mood's name, or the custom free text. Never the literal
    /// "Custom" sentinel — a custom mood serializes as its words.
    /// </summary>
    public string Value => IsCustom ? CustomValue! : _type.Name;

    /// <summary>The user-facing label (same as <see cref="Value"/>).</summary>
    public string Display => Value;

    /// <summary>The known mood keys a user can pick (excludes Custom), in display order.</summary>
    public static IReadOnlyList<string> KnownKeys { get; } =
        MoodType.List.Where(m => m != MoodType.Custom).OrderBy(m => m.Value).Select(m => m.Name).ToList();

    /// <summary>A Custom mood carrying the user's own words.</summary>
    public static Mood Custom(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Mood(MoodType.Custom, value.Trim());
    }

    /// <summary>
    /// Resolves a single string to a Mood (PRD-0006): a value matching a known mood name (case-insensitive)
    /// becomes that known Mood, anything else becomes a Custom Mood carrying the text. Never throws and
    /// never yields the "Custom" sentinel. Blank input throws (a Mood must have content).
    /// </summary>
    public static Mood Resolve(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        return MoodType.TryFromName(trimmed, ignoreCase: true, out var type) && type != MoodType.Custom
            ? new Mood(type, null)
            : Custom(trimmed);
    }

    /// <summary>Non-throwing <see cref="Resolve"/>: false only when the input is blank.</summary>
    public static bool TryResolve(string? value, out Mood mood)
    {
        mood = null!;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        mood = Resolve(value);
        return true;
    }

    /// <summary>
    /// The app-defined mood set (CONTEXT.md) as a SmartEnum — a private implementation detail of
    /// <see cref="Mood"/>. Persisted by name, so values are display-stable and reordering is safe.
    /// </summary>
    private sealed class MoodType : SmartEnum<MoodType>
    {
        public static readonly MoodType Joyful = new(nameof(Joyful), 1);
        public static readonly MoodType Content = new(nameof(Content), 2);
        public static readonly MoodType Calm = new(nameof(Calm), 3);
        public static readonly MoodType Neutral = new(nameof(Neutral), 4);
        public static readonly MoodType Tired = new(nameof(Tired), 5);
        public static readonly MoodType Anxious = new(nameof(Anxious), 6);
        public static readonly MoodType Sad = new(nameof(Sad), 7);
        public static readonly MoodType Angry = new(nameof(Angry), 8);
        public static readonly MoodType Excited = new(nameof(Excited), 9);
        public static readonly MoodType Grateful = new(nameof(Grateful), 10);

        /// <summary>The free-text mood: carries the user's own words on the Mood value object.</summary>
        public static readonly MoodType Custom = new(nameof(Custom), 0);

        private MoodType(string name, int value) : base(name, value) { }
    }
}
