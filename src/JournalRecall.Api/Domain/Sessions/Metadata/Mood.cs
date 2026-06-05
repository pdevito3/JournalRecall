namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// How the user felt during a Session (CONTEXT.md): a <see cref="MoodType"/> — one of the known moods,
/// or <see cref="MoodType.Custom"/> which additionally carries a free-text value. A small value object,
/// persisted on the Session as the scalar mood key (<see cref="MoodType.Name"/>) + optional custom text,
/// not as its own table.
/// </summary>
public sealed record Mood
{
    /// <summary>The mood as a SmartEnum member — the type-safe core of this value object.</summary>
    public MoodType Type { get; }

    /// <summary>The user's free text for a <see cref="MoodType.Custom"/> mood; null for a known mood.</summary>
    public string? CustomValue { get; }

    private Mood(MoodType type, string? customValue)
    {
        Type = type;
        CustomValue = customValue;
    }

    /// <summary>
    /// Builds a validated Mood from a <see cref="MoodType"/>, requiring free text for
    /// <see cref="MoodType.Custom"/> and rejecting it for a known mood.
    /// </summary>
    public static Mood Of(MoodType type, string? customValue = null)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsCustom)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(customValue);
            return new Mood(type, customValue.Trim());
        }

        return new Mood(type, null);
    }

    /// <summary>
    /// Builds a validated Mood from a string key (the boundary form used by DTOs/AI), or throws when the
    /// key is unknown or a Custom mood is missing its text.
    /// </summary>
    public static Mood Of(string key, string? customValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!MoodType.TryFromName(key.Trim(), ignoreCase: true, out var type))
            throw new ArgumentException($"Unknown mood '{key}'.", nameof(key));

        return Of(type, customValue);
    }

    /// <summary>Non-throwing <see cref="Of(string, string?)"/>: false when the key is unknown or Custom lacks text.</summary>
    public static bool TryOf(string? key, string? customValue, out Mood mood)
    {
        mood = null!;
        if (string.IsNullOrWhiteSpace(key) || !MoodType.TryFromName(key.Trim(), ignoreCase: true, out var type))
            return false;

        try
        {
            mood = Of(type, customValue);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>The persisted/serialized key — the <see cref="MoodType.Name"/>.</summary>
    public string Key => Type.Name;

    /// <summary>The user-facing label: the free text for a Custom mood, else the known mood's name.</summary>
    public string Display => Type.IsCustom ? CustomValue ?? MoodType.Custom.Name : Type.Name;
}
