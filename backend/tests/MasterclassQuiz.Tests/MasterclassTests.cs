using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class MasterclassTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly QuizEngine _engine = new(sqlServer.Connection, new FakeTimeProvider(Now));

    [Fact]
    public async Task A_new_masterclass_is_a_draft_without_a_join_code()
    {
        var course = await ImportCourseAsync();

        var created = await _engine.CreateMasterclassAsync(course.CourseId, "Dev team 2026");

        Assert.Empty(created.Problems);
        var masterclass = await _engine.GetMasterclassAsync(created.Masterclass!.Id);
        Assert.NotNull(masterclass);
        Assert.Equal(
            ("Dev team 2026", course.CourseId, course.CourseTitle, MasterclassStatus.Draft, (string?)null, 0, Now.UtcDateTime),
            (masterclass.Name, masterclass.CourseId, masterclass.CourseTitle, masterclass.Status, masterclass.JoinCode, masterclass.Participants, masterclass.CreatedAt));
        Assert.Contains(await _engine.ListMasterclassesAsync(), m => m.Id == masterclass.Id);
    }

    [Fact]
    public async Task A_masterclass_needs_a_name_and_an_existing_course()
    {
        var created = await _engine.CreateMasterclassAsync(courseId: -1, "  ");

        Assert.Null(created.Masterclass);
        Assert.Equal(["The Masterclass needs a name.", "There is no such Course."], created.Problems);
    }

    private async Task<CourseImportSummary> ImportCourseAsync()
    {
        var result = await _engine.ImportCourseAsync(ImportFiles.Example(ImportFiles.UniqueSlug()).ToJsonString());
        return result.Summary!;
    }
}
