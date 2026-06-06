using Ardalis.SmartEnum;
using JournalRecall.Api.Exceptions;

namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// How the user felt during a Session (CONTEXT.md): one of the app-defined known moods, or a Custom
/// mood that additionally carries free text. A value object built on a private <see cref="MoodType"/>
/// SmartEnum — the outside world deals only in <see cref="Mood"/> and its string <see cref="Key"/>.
/// Persisted on the Session as the scalar key + optional custom text, not as its own table.
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

    /// <summary>The persisted/serialized key — a known mood name, or "Custom".</summary>
    public string Key => _type.Name;

    /// <summary>True when this is the free-text Custom mood.</summary>
    public bool IsCustom => _type == MoodType.Custom;

    /// <summary>The user-facing label: the free text for a Custom mood, else the known mood's name.</summary>
    public string Display => IsCustom ? CustomValue ?? MoodType.Custom.Name : _type.Name;

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
    /// Builds a validated Mood from a string key (the boundary form used by DTOs/AI). Throws
    /// <see cref="InvalidSmartEnumPropertyName"/> (→ 422) when the key is missing or unknown, and
    /// <see cref="ValidationException"/> (→ 422) when "Custom" is supplied without free text. A known key
    /// ignores any custom text.
    /// </summary>
    public static Mood Of(string key, string? customValue = null)
    {
        if (string.IsNullOrWhiteSpace(key) || !MoodType.TryFromName(key.Trim(), ignoreCase: true, out var type))
            throw new InvalidSmartEnumPropertyName(nameof(Mood), key, KnownKeys);

        if (type == MoodType.Custom)
        {
            if (string.IsNullOrWhiteSpace(customValue))
                throw new ValidationException("mood", "A custom mood requires text.");
            return Custom(customValue);
        }

        return new Mood(type, null);
    }

    /// <summary>Non-throwing <see cref="Of(string, string?)"/>: false when the key is unknown or Custom lacks text.</summary>
    public static bool TryOf(string? key, string? customValue, out Mood mood)
    {
        mood = null!;
        if (string.IsNullOrWhiteSpace(key) || !MoodType.TryFromName(key.Trim(), ignoreCase: true, out _))
            return false;

        try
        {
            mood = Of(key, customValue);
            return true;
        }
        catch (Exception ex) when (ex is ValidationException or InvalidSmartEnumPropertyName)
        {
            return false;
        }
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
