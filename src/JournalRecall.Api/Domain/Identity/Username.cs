using System.Text.RegularExpressions;
using JournalRecall.Api.Exceptions;

namespace JournalRecall.Api.Domain.Identity;

/// <summary>
/// A user's login handle (issue 0027) — the sole identity now that email is gone. A value object over a
/// single trimmed <see cref="Value"/> string, with a throwing <see cref="Create"/> factory that is the
/// one source of truth for username <strong>format + length</strong>. On violation it throws
/// <see cref="ValidationException"/> keyed on <c>"username"</c>, so the ProblemDetails pipeline maps it
/// to 422 and the frontend surfaces it inline on the field (the established Mood/Location throwing-factory
/// pattern). <strong>Uniqueness is deliberately out of scope</strong> — that needs a store lookup and
/// stays with ASP.NET Identity's normalized-username index.
/// </summary>
public sealed partial class Username : ValueObject
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    public string Value { get; }

    private Username(string value) => Value = value;

    /// <summary>
    /// Builds a validated Username: trims the input, then enforces charset <c>[a-zA-Z0-9._-]</c> and
    /// length <see cref="MinLength"/>–<see cref="MaxLength"/>. Throws <see cref="ValidationException"/>
    /// (keyed on <c>"username"</c>) on any violation, including null/whitespace.
    /// </summary>
    public static Username Create(string? input)
    {
        var trimmed = (input ?? string.Empty).Trim();

        if (trimmed.Length is < MinLength or > MaxLength)
            throw new ValidationException("username",
                $"Username must be between {MinLength} and {MaxLength} characters.");

        if (!AllowedPattern().IsMatch(trimmed))
            throw new ValidationException("username",
                "Username may contain only letters, numbers, and the characters . _ -");

        return new Username(trimmed);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-zA-Z0-9._-]+$")]
    private static partial Regex AllowedPattern();
}
