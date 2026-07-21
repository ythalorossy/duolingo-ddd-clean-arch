using BuildingBlocks.Mediator;

namespace Learning.Application;

public sealed record GetLesson(Guid LessonId) : IRequest<LessonPresentationDto?>;

public sealed record LessonPresentationDto(
    Guid Id, string Title, bool IsPublished, IReadOnlyList<ExercisePresentationDto> Exercises);
public sealed record ExercisePresentationDto(
    Guid Id, int Position, string Prompt, IReadOnlyList<string> Choices); // no correct answer

// Read-model port (returns a DTO) — distinct from the aggregate repositories.
public interface ILessonPresentationRead
{
    Task<LessonPresentationDto?> GetLessonAsync(Guid lessonId, CancellationToken ct);
}

public sealed class GetLessonHandler(ILessonPresentationRead read) : IRequestHandler<GetLesson, LessonPresentationDto?>
{
    public Task<LessonPresentationDto?> HandleAsync(GetLesson request, CancellationToken ct) =>
        read.GetLessonAsync(request.LessonId, ct);
}
