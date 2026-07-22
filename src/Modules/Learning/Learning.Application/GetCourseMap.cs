using BuildingBlocks.Mediator;

namespace Learning.Application;

public sealed record GetCourseMap(Guid CourseId, Guid LearnerId) : IRequest<CourseMapDto?>;

public sealed record CourseMapDto(Guid CourseId, string Title, IReadOnlyList<UnitMapDto> Units);
public sealed record UnitMapDto(Guid Id, string Title, int Position, IReadOnlyList<LessonNodeDto> Lessons);
public sealed record LessonNodeDto(Guid Id, string Title, int Position, string Status);

// Read-model port (returns a DTO) — distinct from the aggregate repositories. Implemented in Infrastructure.
public interface ICourseMapReadService
{
    Task<CourseMapDto?> GetCourseMapAsync(Guid courseId, Guid learnerId, CancellationToken ct);
}

public sealed class GetCourseMapHandler(ICourseMapReadService read)
    : IRequestHandler<GetCourseMap, CourseMapDto?>
{
    public Task<CourseMapDto?> HandleAsync(GetCourseMap request, CancellationToken ct) =>
        read.GetCourseMapAsync(request.CourseId, request.LearnerId, ct);
}
