namespace Learning.Domain;

// Write port owned by the Domain; implemented in Infrastructure. Slice 2 is Learning's first write path.
public interface IAttemptRepository
{
    Task AddAsync(Attempt attempt, CancellationToken ct);
}
