using System.Security.Claims;
using MasterclassQuiz.Api.Participants;
using MasterclassQuiz.Api.Quiz;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MasterclassQuiz.Api.Live;

// Recovery: nothing is lost when a phone or the projector reloads, or the backend restarts.
public sealed partial class QuizHub
{
    /// <summary>
    /// Every client calls this on each connect and reconnect, since SignalR doesn't replay missed messages:
    /// follows the Masterclass's broadcasts and returns what to show now (<see cref="CurrentState"/>; never
    /// the correct option of an unrevealed Question). Open to the Host (projector) and to the Masterclass's
    /// Participants, whose reply includes their own Answer to the Question shown. Null for anyone else, or
    /// if there is no such Masterclass.
    /// </summary>
    public async Task<CurrentState?> GetCurrentState(int masterclassId)
    {
        var token = Context.GetHttpContext()?.Request.Cookies[ParticipantEndpoints.CookieName];
        var state = await engine.GetCurrentStateAsync(masterclassId, string.IsNullOrEmpty(token) ? null : token, Context.ConnectionAborted);
        if (state is null || (state.Participant is null && !IsHost(Context.User)))
        {
            return null;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, MasterclassGroup(masterclassId), Context.ConnectionAborted);
        return state;
    }

    /// <summary>
    /// Host only (the projector's Resume button): makes the backend timer follow the Masterclass's running
    /// Session again, as it does on startup. An overdue Question is revealed and the Session carries on;
    /// following a Session that is already followed changes nothing. Returns the state as it is now.
    /// </summary>
    [Authorize]
    public async Task<CurrentState?> ResumeSession(int masterclassId)
    {
        foreach (var sessionId in await engine.ListRunningSessionsAsync(masterclassId, Context.ConnectionAborted))
        {
            runner.Follow(sessionId);
            runner.Wake(sessionId);
        }
        return await engine.GetCurrentStateAsync(masterclassId, null, Context.ConnectionAborted);
    }

    private static bool IsHost(ClaimsPrincipal? user) => user?.Identity?.IsAuthenticated == true;
}
