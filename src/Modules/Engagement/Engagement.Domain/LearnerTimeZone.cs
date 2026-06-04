using BuildingBlocks.Domain;

namespace Engagement.Domain;

public sealed class LearnerTimeZone : ValueObject
{
    private readonly TimeZoneInfo _timeZone;

    public string IanaId { get; }

    public static LearnerTimeZone Utc => new("UTC");

    public LearnerTimeZone(string ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId))
            throw new ArgumentException("Time zone id is required.", nameof(ianaId));

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException($"Unknown time zone '{ianaId}'.", nameof(ianaId));
        }

        IanaId = ianaId;
    }

    public DateOnly LocalDateOf(DateTimeOffset utcInstant)
    {
        var local = TimeZoneInfo.ConvertTime(utcInstant, _timeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IanaId;
    }
}
