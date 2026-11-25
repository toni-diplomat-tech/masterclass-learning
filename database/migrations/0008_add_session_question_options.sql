-- The options of each Session Question as they were when it started, so a later re-import that edits,
-- moves or retires options never changes what was shown, how Answers were judged, the reveal or a Recap.
-- dbo.StartNextQuestion copies the Question's current options here as the Question starts; from then on
-- the Session reads options (and which one is correct) only from here.
CREATE TABLE dbo.SessionQuestionOptions (
    SessionQuestionId int            NOT NULL CONSTRAINT FK_SessionQuestionOptions_SessionQuestions REFERENCES dbo.SessionQuestions (Id),
    Position          tinyint        NOT NULL CONSTRAINT CK_SessionQuestionOptions_Position CHECK (Position BETWEEN 1 AND 4),
    Text              nvarchar(1000) NOT NULL,
    IsCorrect         bit            NOT NULL,
    CONSTRAINT PK_SessionQuestionOptions PRIMARY KEY (SessionQuestionId, Position)
);

-- Questions that started before this table existed get the options they have now, the best record left.
INSERT dbo.SessionQuestionOptions (SessionQuestionId, Position, Text, IsCorrect)
SELECT sq.Id, o.Position, o.Text, o.IsCorrect
FROM dbo.SessionQuestions sq
JOIN dbo.QuestionOptions o ON o.QuestionId = sq.QuestionId AND o.RetiredAt IS NULL
WHERE sq.StartedAt IS NOT NULL;
