namespace Host;

public sealed class LeagueSettlementOptions
{
    // Feature flag for the hosted scheduler. Default on; the E2E test hosts turn it off.
    public bool Enabled { get; set; } = true;

    // How often the scheduler checks for ended-but-unsettled weeks.
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);
}
