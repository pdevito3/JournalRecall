using Ardalis.SmartEnum;

namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// The app-defined set of moods (CONTEXT.md) as a SmartEnum: the ten known moods a user can pick, plus
/// the <see cref="Custom"/> member that carries the user's own free text (held on the <see cref="Mood"/>
/// value object). Persisted by <see cref="SmartEnum{TEnum}.Name"/>, so values are display-stable and
/// reordering them never breaks stored data.
/// </summary>
public sealed class MoodType : SmartEnum<MoodType>
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

    /// <summary>The free-text mood: not a fixed feeling, it carries the user's own words on the value object.</summary>
    public static readonly MoodType Custom = new(nameof(Custom), 0);

    private MoodType(string name, int value) : base(name, value) { }

    /// <summary>True for the free-text member (which requires a custom value on the <see cref="Mood"/>).</summary>
    public bool IsCustom => this == Custom;

    /// <summary>The pickable known moods (everything except <see cref="Custom"/>), in declared order.</summary>
    public static IReadOnlyList<MoodType> Known => List.Where(m => m != Custom).OrderBy(m => m.Value).ToList();
}
