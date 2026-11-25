using System.Text;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Api.Courses;

/// <summary>The Host's Course endpoints: thin callers of the Quiz engine.</summary>
public static class CourseEndpoints
{
    /// <summary>
    /// Maps <c>GET courses</c> (the Course list) and <c>POST courses/import</c> (the request body is the
    /// import file's contents) on the Host's <c>/api/host</c> group, which requires the Host session.
    /// </summary>
    public static RouteGroupBuilder MapCourseEndpoints(this RouteGroupBuilder host)
    {
        host.MapGet("/courses", (QuizEngine engine, CancellationToken cancellationToken) =>
            engine.ListCoursesAsync(cancellationToken));

        host.MapPost("/courses/import", async (HttpRequest request, QuizEngine engine, CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var contents = await reader.ReadToEndAsync(cancellationToken);

            var result = await engine.ImportCourseAsync(contents, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Summary)
                : Results.Problem(
                    title: "The import file was rejected.",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["problems"] = result.Problems });
        });

        return host;
    }
}
