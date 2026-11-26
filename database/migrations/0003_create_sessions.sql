-- Sessions: the one live quiz per Workshop within a Masterclass, and the Questions selected for it.
-- EasyCount/MediumCount/HardCount are the Difficulty mix the Host asked for; the selected Questions can
-- differ in Difficulty where the fallback filled a gap. EndedAt is set when the last Question is revealed.

CREATE TABLE dbo.Sessions (
    Id            int IDENTITY CONSTRAINT PK_Sessions PRIMARY KEY,
    MasterclassId int          NOT NULL CONSTRAINT FK_Sessions_Masterclasses REFERENCES dbo.Masterclasses (Id),
    WorkshopId    int          NOT NULL CONSTRAINT FK_Sessions_Workshops REFERENCES dbo.Workshops (Id),
    EasyCount     int          NOT NULL CONSTRAINT CK_Sessions_EasyCount CHECK (EasyCount >= 0),
    MediumCount   int          NOT NULL CONSTRAINT CK_Sessions_MediumCount CHECK (MediumCount >= 0),
    HardCount     int          NOT NULL CONSTRAINT CK_Sessions_HardCount CHECK (HardCount >= 0),
    StartedAt     datetime2(7) NOT NULL,
    EndedAt       datetime2(7) NULL,
    CONSTRAINT UQ_Sessions_MasterclassId_WorkshopId UNIQUE (MasterclassId, WorkshopId)
);

-- A Session's Questions in the order they run (Position 1 first), each at most once. TimeLimitMs is the
-- time to answer for the Question's Difficulty, taken from configuration when the Session started.
-- StartedAt and Deadline are set when the Question starts, RevealedAt when it is revealed.
CREATE TABLE dbo.SessionQuestions (
    Id          int IDENTITY CONSTRAINT PK_SessionQuestions PRIMARY KEY,
    SessionId   int          NOT NULL CONSTRAINT FK_SessionQuestions_Sessions REFERENCES dbo.Sessions (Id),
    Position    int          NOT NULL CONSTRAINT CK_SessionQuestions_Position CHECK (Position >= 1),
    QuestionId  int          NOT NULL CONSTRAINT FK_SessionQuestions_Questions REFERENCES dbo.Questions (Id),
    TimeLimitMs int          NOT NULL CONSTRAINT CK_SessionQuestions_TimeLimitMs CHECK (TimeLimitMs > 0),
    StartedAt   datetime2(7) NULL,
    Deadline    datetime2(7) NULL,
    RevealedAt  datetime2(7) NULL,
    CONSTRAINT UQ_SessionQuestions_SessionId_Position UNIQUE (SessionId, Position),
    CONSTRAINT UQ_SessionQuestions_SessionId_QuestionId UNIQUE (SessionId, QuestionId),
    CONSTRAINT CK_SessionQuestions_Started CHECK ((StartedAt IS NULL AND Deadline IS NULL) OR (StartedAt IS NOT NULL AND Deadline > StartedAt)),
    CONSTRAINT CK_SessionQuestions_Revealed CHECK (RevealedAt IS NULL OR StartedAt IS NOT NULL)
);
