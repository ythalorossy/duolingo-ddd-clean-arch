using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Title : ValueObject
{
    public const int MaxLength = 120;

    public string Value { get; }

    public Title(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Title cannot be empty.", nameof(value));

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Title cannot exceed {MaxLength} characters.", nameof(value));

        Value = trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
