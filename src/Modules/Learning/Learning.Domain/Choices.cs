using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Choices : ValueObject
{
    public IReadOnlyList<string> Values { get; }

    public Choices(IReadOnlyList<string> values)
    {
        if (values is null || values.Count < 2)
            throw new ArgumentException("A multiple-choice exercise needs at least two options.", nameof(values));
        if (values.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Choice options cannot be empty.", nameof(values));

        Values = values.Select(v => v.Trim()).ToArray();
    }

    public int Count => Values.Count;
    public string this[int index] => Values[index];
    public bool IsValidIndex(int index) => index >= 0 && index < Values.Count;

    protected override IEnumerable<object?> GetEqualityComponents() => Values;
    public override string ToString() => string.Join(" | ", Values);
}
