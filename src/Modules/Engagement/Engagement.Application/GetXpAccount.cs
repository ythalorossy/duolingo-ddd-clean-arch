using BuildingBlocks.Mediator;
using Engagement.Domain;

namespace Engagement.Application;

public sealed record GetXpAccount(Guid LearnerId) : IRequest<XpAccountDto>;

public sealed record XpAccountDto(Guid LearnerId, int TotalXp);

public sealed class GetXpAccountHandler(IXpAccountRepository repository)
    : IRequestHandler<GetXpAccount, XpAccountDto>
{
    public async Task<XpAccountDto> HandleAsync(GetXpAccount request, CancellationToken ct)
    {
        var learner = await repository.GetAsync(new LearnerId(request.LearnerId), ct);
        return new XpAccountDto(request.LearnerId, learner?.TotalXp.Value ?? 0);
    }
}
