using Microsoft.Extensions.DependencyInjection;

namespace Learning.Stub;

public static class LearningStubExtensions
{
    // Handlers are picked up by AddMediator(assembly); this marker keeps the
    // assembly easy to reference from the Host's mediator registration.
    public static readonly System.Reflection.Assembly Assembly = typeof(CompleteLesson).Assembly;
}
