-- Answers to a Session's Questions, and when each Question stops taking them.

-- Answers are accepted up to 500 ms after the deadline, for network delay. This is the only place
-- that grace is defined: procedures compare @Now with AnswersCloseAt.
ALTER TABLE dbo.SessionQuestions
    ADD AnswersCloseAt AS DATEADD(millisecond, 500, Deadline) PERSISTED;

-- One Answer per Participant per Session Question, never changed. Outcome: 1 Correct, 2 Incorrect,
-- 3 Timed out (recorded at the reveal for a Participant who was in the Masterclass when the Question
-- started but didn't answer; it has no option or time). OptionPosition is the chosen option
-- (dbo.QuestionOptions.Position of the Question). ElapsedMs = AnsweredAt - the Question's StartedAt;
-- it can exceed the time limit by up to the grace.
CREATE TABLE dbo.Answers (
    Id                int IDENTITY CONSTRAINT PK_Answers PRIMARY KEY,
    SessionQuestionId int          NOT NULL CONSTRAINT FK_Answers_SessionQuestions REFERENCES dbo.SessionQuestions (Id),
    ParticipantId     int          NOT NULL CONSTRAINT FK_Answers_Participants REFERENCES dbo.Participants (Id),
    Outcome           tinyint      NOT NULL CONSTRAINT CK_Answers_Outcome CHECK (Outcome BETWEEN 1 AND 3),
    OptionPosition    tinyint      NULL,
    AnsweredAt        datetime2(7) NULL,
    ElapsedMs         int          NULL,
    CONSTRAINT UQ_Answers_SessionQuestionId_ParticipantId UNIQUE (SessionQuestionId, ParticipantId),
    CONSTRAINT CK_Answers_Answered CHECK (
        (Outcome = 3 AND OptionPosition IS NULL AND AnsweredAt IS NULL AND ElapsedMs IS NULL)
        OR (Outcome IN (1, 2) AND OptionPosition IS NOT NULL AND AnsweredAt IS NOT NULL AND ElapsedMs >= 0))
);
