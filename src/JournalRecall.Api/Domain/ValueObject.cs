using System.Reflection;

namespace JournalRecall.Api.Domain;

/// <summary>
/// Base for immutable value objects: equality is <em>structural</em> — two instances of the same type
/// are equal when all of their public properties and fields are equal (the reflection-based jhewlett
/// pattern). Derive from this to get value equality, <c>==</c>/<c>!=</c>, and a matching
/// <see cref="GetHashCode"/> for free; opt a member out of the comparison with
/// <see cref="IgnoreMemberAttribute"/>. Existing value objects (<c>Mood</c>, <c>Location</c>) predate
/// this primitive and are intentionally not retrofitted.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    private List<PropertyInfo>? _properties;
    private List<FieldInfo>? _fields;

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (ReferenceEquals(left, null))
            return ReferenceEquals(right, null);
        return left.Equals(right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);

    public bool Equals(ValueObject? other) => Equals((object?)other);

    public override bool Equals(object? obj)
    {
        if (obj is null || GetType() != obj.GetType())
            return false;

        return GetProperties().All(p => Equals(p.GetValue(this), p.GetValue(obj)))
            && GetFields().All(f => Equals(f.GetValue(this), f.GetValue(obj)));
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var property in GetProperties())
                hash = hash * 23 + (property.GetValue(this)?.GetHashCode() ?? 0);
            foreach (var field in GetFields())
                hash = hash * 23 + (field.GetValue(this)?.GetHashCode() ?? 0);
            return hash;
        }
    }

    private IEnumerable<PropertyInfo> GetProperties() =>
        _properties ??= GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => !Attribute.IsDefined(p, typeof(IgnoreMemberAttribute)))
            .ToList();

    private IEnumerable<FieldInfo> GetFields() =>
        _fields ??= GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(f => !Attribute.IsDefined(f, typeof(IgnoreMemberAttribute)))
            .ToList();
}

/// <summary>Excludes a public property or field from a <see cref="ValueObject"/>'s structural equality.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class IgnoreMemberAttribute : Attribute;
