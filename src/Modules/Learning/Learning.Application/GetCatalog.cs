using BuildingBlocks.Mediator;

namespace Learning.Application;

public sealed record GetCatalog() : IRequest<CatalogDto>;

public sealed record CatalogDto(IReadOnlyList<CourseDto> Courses);
public sealed record CourseDto(Guid Id, string Title, string? Language, IReadOnlyList<UnitDto> Units);
public sealed record UnitDto(Guid Id, string Title, int Position, IReadOnlyList<LessonDto> Lessons);
public sealed record LessonDto(Guid Id, string Title, int Position, bool IsPublished);

// Read-model port (returns a DTO) — distinct from the aggregate repository. Implemented in Infrastructure.
public interface ICatalogReadService
{
    Task<CatalogDto> GetCatalogAsync(CancellationToken ct);
}

public sealed class GetCatalogHandler(ICatalogReadService catalog) : IRequestHandler<GetCatalog, CatalogDto>
{
    public Task<CatalogDto> HandleAsync(GetCatalog request, CancellationToken ct) => catalog.GetCatalogAsync(ct);
}
