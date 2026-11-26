-- Masterclasses (one group taught a Course) and their Participants.
-- Status: 1 Draft, 2 Open, 3 Closed. A Join code is set when the Masterclass opens and stays set
-- afterwards; it is unique only among Open Masterclasses, so closing frees it for reuse.

CREATE TABLE dbo.Masterclasses (
    Id        int IDENTITY CONSTRAINT PK_Masterclasses PRIMARY KEY,
    CourseId  int           NOT NULL CONSTRAINT FK_Masterclasses_Courses REFERENCES dbo.Courses (Id),
    Name      nvarchar(200) NOT NULL,
    Status    tinyint       NOT NULL CONSTRAINT CK_Masterclasses_Status CHECK (Status BETWEEN 1 AND 3),
    JoinCode  char(6)       NULL,
    CreatedAt datetime2(7)  NOT NULL,
    OpenedAt  datetime2(7)  NULL,
    ClosedAt  datetime2(7)  NULL,
    CONSTRAINT CK_Masterclasses_JoinCode CHECK (JoinCode NOT LIKE '%[^0-9]%' AND LEN(JoinCode) = 6),
    CONSTRAINT CK_Masterclasses_OpenHasJoinCode CHECK (Status = 1 OR JoinCode IS NOT NULL)
);

CREATE UNIQUE INDEX UQ_Masterclasses_JoinCode_Open ON dbo.Masterclasses (JoinCode) WHERE Status = 2;

-- A Participant is known by the SHA-256 hash of the random token in their browser's cookie; the
-- token itself is never stored. Nicknames are unique within a Masterclass.
CREATE TABLE dbo.Participants (
    Id            int IDENTITY CONSTRAINT PK_Participants PRIMARY KEY,
    MasterclassId int           NOT NULL CONSTRAINT FK_Participants_Masterclasses REFERENCES dbo.Masterclasses (Id),
    TokenHash     binary(32)    NOT NULL CONSTRAINT UQ_Participants_TokenHash UNIQUE,
    Nickname      nvarchar(100) NOT NULL,
    JoinedAt      datetime2(7)  NOT NULL,
    CONSTRAINT UQ_Participants_MasterclassId_Nickname UNIQUE (MasterclassId, Nickname)
);
