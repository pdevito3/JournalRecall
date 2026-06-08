using Ardalis.SmartEnum;

namespace JournalRecall.Api.Domain.Sessions.Metadata;

/// <summary>
/// What the user was physically doing <em>while journaling</em> (CONTEXT.md, PRD-0007): one of the
/// app-defined known activities, or a Custom activity carrying the user's own words. Single-valued and
/// <b>non-nullable</b> — a Session always has exactly one Activity, defaulting to <see cref="None"/>
/// ("didn't say / not applicable", the zero value, distinct from <c>Stationary</c>'s "deliberately sitting
/// still"). There is no <c>null</c>/unset state.
///
/// A value object built on a private <see cref="ActivityType"/> SmartEnum whose <b>sole persisted state is
/// the canonical <see cref="Value"/> string</b>; known-ness is derived on demand by resolving Value against
/// the known set, never stored. That single-scalar shape is what lets persistence map it as a
/// <c>ComplexProperty</c> with no <c>ValueConverter</c>. UserSet only — the AI never proposes or sets it.
/// </summary>
public sealed record Activity
{
    /// <summary>
    /// The sole persisted scalar: a known activity's canonical name, <c>"None"</c>, or the custom free
    /// text. Never the literal <c>"Custom"</c> sentinel — a custom activity serializes as the user's words.
    /// </summary>
    public string Value { get; }

    // EF rematerializes the complex type by binding the `activity` column to this constructor parameter.
    private Activity(string value) => Value = value;

    // Resolve the persisted Value back to a known member on demand — nothing but Value is stored.
    private ActivityType Type =>
        ActivityType.TryFromName(Value, ignoreCase: true, out var type) ? type : ActivityType.Custom;

    /// <summary>True when <see cref="Value"/> names a known activity (including None); false for custom text.</summary>
    public bool IsKnown => Type != ActivityType.Custom;

    /// <summary>True for the zero value (<see cref="None"/>) — "didn't say / not applicable".</summary>
    public bool IsNone => Type == ActivityType.None;

    /// <summary>True when this is a free-text custom activity (not a known member).</summary>
    public bool IsCustom => Type == ActivityType.Custom;

    /// <summary>The user-facing label (same as <see cref="Value"/>).</summary>
    public string Display => Value;

    /// <summary>The zero value: "didn't say / not applicable", distinct from Stationary. The Session default.</summary>
    public static Activity None { get; } = new(ActivityType.None.Name);

    /// <summary>The known activity keys a user can pick (includes None, excludes the Custom sentinel), in order.</summary>
    public static IReadOnlyList<string> KnownKeys { get; } =
        ActivityType.List.Where(a => a != ActivityType.Custom).OrderBy(a => a.Value).Select(a => a.Name).ToList();

    /// <summary>A Custom activity carrying the user's own words.</summary>
    public static Activity Custom(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Activity(value.Trim());
    }

    /// <summary>
    /// Resolves a string to an Activity (PRD-0007): a value matching a known activity name
    /// (case-insensitive) becomes that known Activity in canonical case; blank/absent input yields
    /// <see cref="None"/>; anything else becomes a Custom Activity carrying the raw text. Never throws and
    /// never yields the "Custom" sentinel as <see cref="Value"/>.
    /// </summary>
    public static Activity Resolve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return None;
        var trimmed = value.Trim();
        return ActivityType.TryFromName(trimmed, ignoreCase: true, out var type) && type != ActivityType.Custom
            ? new Activity(type.Name)
            : Custom(trimmed);
    }

    /// <summary>
    /// The app-defined activity set (CONTEXT.md) as a SmartEnum — a private detail of <see cref="Activity"/>.
    /// Persisted by canonical name, so values are display-stable and reordering is safe. None is the zero
    /// value; Custom is the free-text sentinel (never persisted as the literal name).
    /// </summary>
    private sealed class ActivityType : SmartEnum<ActivityType>
    {
        public static readonly ActivityType None = new(nameof(None), 0);
        public static readonly ActivityType Stationary = new(nameof(Stationary), 1);
        public static readonly ActivityType Walking = new(nameof(Walking), 2);
        public static readonly ActivityType Eating = new(nameof(Eating), 3);
        public static readonly ActivityType Commuting = new(nameof(Commuting), 4);
        public static readonly ActivityType Exercising = new(nameof(Exercising), 5);
        public static readonly ActivityType Resting = new(nameof(Resting), 6);

        /// <summary>The free-text activity: carries the user's own words on the Activity value object.</summary>
        public static readonly ActivityType Custom = new(nameof(Custom), 7);

        private ActivityType(string name, int value) : base(name, value) { }
    }
}
