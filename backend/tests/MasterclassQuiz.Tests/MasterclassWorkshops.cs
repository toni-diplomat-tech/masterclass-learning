using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Tests;

/// <summary>Finding a Workshop of a Masterclass's Course, to start its Session.</summary>
public static class MasterclassWorkshops
{
    /// <summary>The id of the Workshop with this slug in the Course the Masterclass teaches.</summary>
    public static async Task<int> WorkshopIdAsync(this QuizEngine engine, int masterclassId, string slug)
    {
        var courseId = (await engine.GetMasterclassAsync(masterclassId))!.CourseId;
        return (await engine.ListCoursesAsync())
            .Single(c => c.Id == courseId).Workshops
            .Single(w => w.Slug == slug).Id;
    }
}
