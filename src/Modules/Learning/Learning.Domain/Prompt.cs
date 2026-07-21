using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Prompt : ValueObject
{
    public const int MaxLength = 500;

    public string Value { get; }

    public Prompt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Prompt cannot be empty.", nameof(value));

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Prompt cannot exceed {MaxLength} characters.", nameof(value));

        Value = trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
