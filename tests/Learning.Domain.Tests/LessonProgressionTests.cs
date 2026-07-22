using Learning.Domain;
using Xunit;

namespace Learning.Domain.Tests;

public class LessonProgressionTests
{
    private static readonly CourseId Course = new(Guid.NewGuid());
    private static readonly UnitId Unit1 = new(Guid.NewGuid());
    private static readonly UnitId Unit2 = new(Guid.NewGuid());

    private static Unit U(UnitId id, int pos) => Unit.Create(id, Course, new Title($"U{pos}"), pos);
    private static Lesson L(LessonId id, UnitId unit, int pos) =>
        Lesson.Create(id, unit, new Title($"L{pos}"), pos, isPublished: true);

    [Fact]
    public void Empty_course_yields_no_nodes()
    {
        var status = LessonProgression.Classify(new List<Unit>(), new List<Lesson>(), new HashSet<LessonId>());
        Assert.Empty(status);
    }

    [Fact]
    public void Nothing_passed_first_lesson_unlocked_rest_locked()
    {
        var l1 = new LessonId(Guid.NewGuid());
        var l2 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1) };
        var lessons = new[] { L(l1, Unit1, 1), L(l2, Unit1, 2) };

        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId>());

        Assert.Equal(NodeStatus.Unlocked, status[l1]);
        Assert.Equal(NodeStatus.Locked, status[l2]);
    }

    [Fact]
    public void Passing_the_first_lesson_completes_it_and_unlocks_the_second()
    {
        var l1 = new LessonId(Guid.NewGuid());
        var l2 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1) };
        var lessons = new[] { L(l1, Unit1, 1), L(l2, Unit1, 2) };

        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId> { l1 });

        Assert.Equal(NodeStatus.Completed, status[l1]);
        Assert.Equal(NodeStatus.Unlocked, status[l2]);
    }

    [Fact]
    public void Completing_a_unit_unlocks_the_next_units_first_lesson()
    {
        var a1 = new LessonId(Guid.NewGuid());
        var b1 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1), U(Unit2, 2) };
        var lessons = new[] { L(a1, Unit1, 1), L(b1, Unit2, 1) };

        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId> { a1 });

        Assert.Equal(NodeStatus.Completed, status[a1]);
        Assert.Equal(NodeStatus.Unlocked, status[b1]);
    }

    [Fact]
    public void Partial_unit_keeps_the_next_unit_locked()
    {
        var a1 = new LessonId(Guid.NewGuid());
        var a2 = new LessonId(Guid.NewGuid());
        var b1 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1), U(Unit2, 2) };
        var lessons = new[] { L(a1, Unit1, 1), L(a2, Unit1, 2), L(b1, Unit2, 1) };

        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId> { a1 });

        Assert.Equal(NodeStatus.Completed, status[a1]);
        Assert.Equal(NodeStatus.Unlocked, status[a2]);
        Assert.Equal(NodeStatus.Locked, status[b1]);
    }

    [Fact]
    public void Out_of_order_pass_is_completed_without_leaking_unlocks()
    {
        var a1 = new LessonId(Guid.NewGuid());
        var a2 = new LessonId(Guid.NewGuid());
        var a3 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1) };
        var lessons = new[] { L(a1, Unit1, 1), L(a2, Unit1, 2), L(a3, Unit1, 3) };

        // a2 passed while a1 is not: a2 Completed, a1 the only Unlocked (frontier), a3 Locked.
        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId> { a2 });

        Assert.Equal(NodeStatus.Unlocked, status[a1]);
        Assert.Equal(NodeStatus.Completed, status[a2]);
        Assert.Equal(NodeStatus.Locked, status[a3]);
    }

    [Fact]
    public void A_unit_with_no_published_lessons_opens_the_next_unit()
    {
        var b1 = new LessonId(Guid.NewGuid());
        var units = new[] { U(Unit1, 1), U(Unit2, 2) }; // Unit1 contributes no published lessons
        var lessons = new[] { L(b1, Unit2, 1) };

        var status = LessonProgression.Classify(units, lessons, new HashSet<LessonId>());

        Assert.Equal(NodeStatus.Unlocked, status[b1]);
        Assert.Single(status);
    }
}
