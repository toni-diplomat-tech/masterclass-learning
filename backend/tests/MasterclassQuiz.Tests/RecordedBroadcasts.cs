using System.Threading.Channels;
using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Tests;

/// <summary>One broadcast as a Masterclass's group would hear it: the message name and its payload.</summary>
public sealed record Broadcast(string Message, int MasterclassId, object Payload);

/// <summary>
/// The broadcast port's recording adapter: keeps every broadcast in order, so a test can await the next
/// one (the module may send from the background) or check that nothing more is sent.
/// </summary>
public sealed class RecordedBroadcasts : IMasterclassBroadcasts
{
    /// <summary>How long <see cref="NextAsync"/> waits by default before failing the test.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    /// <summary>How long <see cref="AssertQuietAsync"/> listens by default.</summary>
    public static readonly TimeSpan DefaultQuietWindow = TimeSpan.FromMilliseconds(200);

    private readonly Channel<Broadcast> _heard = Channel.CreateUnbounded<Broadcast>();

    /// <summary>The next broadcast not yet taken, waiting up to <paramref name="timeout"/> for it.</summary>
    public async Task<Broadcast> NextAsync(TimeSpan? timeout = null)
    {
        var limit = timeout ?? DefaultTimeout;
        using var waiting = new CancellationTokenSource(limit);
        try
        {
            return await _heard.Reader.ReadAsync(waiting.Token);
        }
        catch (OperationCanceledException) when (waiting.IsCancellationRequested)
        {
            throw new TimeoutException($"No broadcast within {limit.TotalMilliseconds} ms.");
        }
    }

    /// <summary>The next broadcast, which must be <paramref name="message"/>; returns its payload.</summary>
    public async Task<T> NextAsync<T>(string message, TimeSpan? timeout = null)
    {
        var next = await NextAsync(timeout);
        Assert.Equal(message, next.Message);
        return Assert.IsType<T>(next.Payload);
    }

    /// <summary>Fails if a broadcast is waiting to be taken or arrives within <paramref name="window"/>.</summary>
    public async Task AssertQuietAsync(TimeSpan? window = null)
    {
        using var listening = new CancellationTokenSource(window ?? DefaultQuietWindow);
        try
        {
            var heard = await _heard.Reader.ReadAsync(listening.Token);
            Assert.Fail($"Expected no broadcast, but heard {heard.Message} for Masterclass {heard.MasterclassId}.");
        }
        catch (OperationCanceledException) when (listening.IsCancellationRequested)
        {
        }
    }

    public Task ParticipantCountUpdatedAsync(ParticipantCount count, CancellationToken cancellationToken = default) =>
        Record(QuizHub.ParticipantCountUpdated, count.MasterclassId, count);

    public Task QuestionStartedAsync(QuestionStarted question, CancellationToken cancellationToken = default) =>
        Record(QuizHub.QuestionStarted, question.MasterclassId, question);

    public Task AnswerCountUpdatedAsync(AnswerCount count, CancellationToken cancellationToken = default) =>
        Record(QuizHub.AnswerCountUpdated, count.MasterclassId, count);

    public Task QuestionRevealedAsync(QuestionRevealed revealed, CancellationToken cancellationToken = default) =>
        Record(QuizHub.QuestionRevealed, revealed.MasterclassId, revealed);

    public Task SessionEndedAsync(SessionEnded ended, CancellationToken cancellationToken = default) =>
        Record(QuizHub.SessionEnded, ended.MasterclassId, ended);

    public Task LeaderboardUpdatedAsync(SessionLeaderboard leaderboard, CancellationToken cancellationToken = default) =>
        Record(QuizHub.LeaderboardUpdated, leaderboard.MasterclassId, leaderboard);

    public Task MasterclassClosedAsync(int masterclassId, CancellationToken cancellationToken = default) =>
        Record(QuizHub.MasterclassClosed, masterclassId, new MasterclassClosedMessage(masterclassId));

    private Task Record(string message, int masterclassId, object payload)
    {
        _heard.Writer.TryWrite(new Broadcast(message, masterclassId, payload));
        return Task.CompletedTask;
    }
}
