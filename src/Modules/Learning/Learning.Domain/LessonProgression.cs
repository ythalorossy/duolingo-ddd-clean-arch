namespace Learning.Domain;

// Rule A — linear within a unit, sequential units. A pure function of (ordered structure, passed set).
// The caller passes PUBLISHED lessons only, so drafts are neither nodes nor gates.
public static class LessonProgression
{
    public static IReadOnlyDictionary<LessonId, NodeStatus> Classify(
        IReadOnlyList<Unit> unitsInOrder,
        IReadOnlyList<Lesson> publishedLessons,
        IReadOnlySet<LessonId> passedLessonIds)
    {
        var lessonsByUnit = publishedLessons
            .GroupBy(l => l.UnitId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Position).ToList());

        var status = new Dictionary<LessonId, NodeStatus>();
        var allPriorUnitsComplete = true; // nothing precedes the first unit

        foreach (var unit in unitsInOrder.OrderBy(u => u.Position))
        {
            var unitOpen = allPriorUnitsComplete;
            var priorLessonsComplete = true; // within this unit
            var thisUnitComplete = true;

            var lessons = lessonsByUnit.TryGetValue(unit.Id, out var found)
                ? found
                : new List<Lesson>();

            foreach (var lesson in lessons)
            {
                if (passedLessonIds.Contains(lesson.Id))
                {
                    status[lesson.Id] = NodeStatus.Completed;
                    continue; // a passed lesson never leaks an unlock and never blocks; it is simply done
                }

                thisUnitComplete = false;
                status[lesson.Id] = unitOpen && priorLessonsComplete
                    ? NodeStatus.Unlocked
                    : NodeStatus.Locked;
                priorLessonsComplete = false; // the frontier is the first unpassed lesson only
            }

            // A unit with no published lessons is vacuously complete, so the next unit still opens.
            allPriorUnitsComplete = allPriorUnitsComplete && thisUnitComplete;
        }

        return status;
    }
}
