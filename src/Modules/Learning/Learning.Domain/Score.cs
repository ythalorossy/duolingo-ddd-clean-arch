using BuildingBlocks.Domain;

namespace Learning.Domain;

public sealed class Score : ValueObject
{
    public int Correct { get; }
    public int Total { get; }

    public Score(int correct, int total)
    {
        if (total < 1)
            throw new ArgumentException("A score needs at least one gradeable exercise.", nameof(total));
        if (correct < 0 || correct > total)
            throw new ArgumentException("Correct count must be between 0 and Total.", nameof(correct));

        Correct = correct;
        Total = total;
    }

    public double Percentage => (double)Correct / Total;

    public bool MeetsThreshold(double threshold) => Percentage >= threshold;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Correct;
        yield return Total;
    }

    public override string ToString() => $"{Correct}/{Total}";
}
