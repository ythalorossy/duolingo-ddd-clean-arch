namespace Host;

public interface ICurrentUser
{
    Guid LearnerId { get; }
}

// Slice 1: no real auth. The learner comes from an "X-Learner-Id" header, or a fixed
// demo learner if absent. Replaced by the real Identity module later.
public sealed class HeaderCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public static readonly Guid DemoLearnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Guid LearnerId
    {
        get
        {
            var header = accessor.HttpContext?.Request.Headers["X-Learner-Id"].ToString();
            return Guid.TryParse(header, out var id) ? id : DemoLearnerId;
        }
    }
}
