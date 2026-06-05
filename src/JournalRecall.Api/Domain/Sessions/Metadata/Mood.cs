namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// How the user felt during a Session (CONTEXT.md): a known mood from the app-defined set, or
/// <see cref="CustomKey"/> which additionally carries a free-text value. A small value object —
/// persisted on the Session as the scalar key + optional custom text, not as its own table.
/// </summary>
public sealed record Mood
{
    /// <summary>The app-defined known moods (the "SmartEnum" set). Not user-extensible.</summary>
    public static readonly IReadOnlyList<string> Known =
        ["Joyful", "Content", "Calm", "Neutral", "Tired", "Anxious", "Sad", "Angry", "Excited", "Grateful"];

    /// <summary>The reserved key whose value is the user's free text.</summary>
    public const string CustomKey = "Custom";

    public string Key { get; }
    public string? CustomValue { get; }

    private Mood(string key, string? customValue)
    {
        Key = key;
        CustomValue = customValue;
    }

    /// <summary>
    /// Builds a validated Mood, or throws when the key is unknown or a Custom mood is missing its text.
    /// </summary>
    public static Mood Of(string key, string? customValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        key = key.Trim();

        if (key.Equals(CustomKey, StringComparison.OrdinalIgnoreCase))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(customValue, nameof(customValue));
            return new Mood(CustomKey, customValue.Trim());
        }

        var known = Known.FirstOrDefault(m => m.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unknown mood '{key}'.", nameof(key));
        return new Mood(known, null);
    }

    /// <summary>The user-facing label: the free text for a Custom mood, else the known key.</summary>
    public string Display => Key == CustomKey ? CustomValue ?? CustomKey : Key;
}
