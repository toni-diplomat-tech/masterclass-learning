-- Course structure and Questions, filled by dbo.ImportCourse from an import file.
-- Nothing is ever deleted by an import: Workshops, Lessons, Questions and options that leave the file
-- are retired (RetiredAt set), so past Answers keep pointing at them.

CREATE TABLE dbo.Courses (
    Id         int IDENTITY CONSTRAINT PK_Courses PRIMARY KEY,
    Slug       nvarchar(100) NOT NULL CONSTRAINT UQ_Courses_Slug UNIQUE,
    Title      nvarchar(200) NOT NULL,
    CreatedAt  datetime2(7)  NOT NULL
);

CREATE TABLE dbo.Workshops (
    Id        int IDENTITY CONSTRAINT PK_Workshops PRIMARY KEY,
    CourseId  int           NOT NULL CONSTRAINT FK_Workshops_Courses REFERENCES dbo.Courses (Id),
    Slug      nvarchar(100) NOT NULL,
    Title     nvarchar(200) NOT NULL,
    Number    int           NOT NULL,
    RetiredAt datetime2(7)  NULL,
    CONSTRAINT UQ_Workshops_CourseId_Slug UNIQUE (CourseId, Slug)
);

-- Lesson slugs are unique within the Course, because Questions name their Lesson by slug alone.
CREATE TABLE dbo.Lessons (
    Id         int IDENTITY CONSTRAINT PK_Lessons PRIMARY KEY,
    CourseId   int            NOT NULL CONSTRAINT FK_Lessons_Courses REFERENCES dbo.Courses (Id),
    WorkshopId int            NOT NULL CONSTRAINT FK_Lessons_Workshops REFERENCES dbo.Workshops (Id),
    Slug       nvarchar(100)  NOT NULL,
    Title      nvarchar(200)  NOT NULL,
    Number     int            NOT NULL,
    Url        nvarchar(2000) NOT NULL,
    RetiredAt  datetime2(7)   NULL,
    CONSTRAINT UQ_Lessons_CourseId_Slug UNIQUE (CourseId, Slug)
);

-- Difficulty: 1 Easy, 2 Medium, 3 Hard. StableId is the Question's id from the import file.
CREATE TABLE dbo.Questions (
    Id         int IDENTITY CONSTRAINT PK_Questions PRIMARY KEY,
    CourseId   int            NOT NULL CONSTRAINT FK_Questions_Courses REFERENCES dbo.Courses (Id),
    StableId   nvarchar(100)  NOT NULL,
    LessonId   int            NOT NULL CONSTRAINT FK_Questions_Lessons REFERENCES dbo.Lessons (Id),
    Difficulty tinyint        NOT NULL CONSTRAINT CK_Questions_Difficulty CHECK (Difficulty BETWEEN 1 AND 3),
    Text       nvarchar(2000) NOT NULL,
    RetiredAt  datetime2(7)   NULL,
    CONSTRAINT UQ_Questions_CourseId_StableId UNIQUE (CourseId, StableId)
);

-- An option is identified by its 1-based position in the Question's option list.
CREATE TABLE dbo.QuestionOptions (
    Id         int IDENTITY CONSTRAINT PK_QuestionOptions PRIMARY KEY,
    QuestionId int            NOT NULL CONSTRAINT FK_QuestionOptions_Questions REFERENCES dbo.Questions (Id),
    Position   tinyint        NOT NULL CONSTRAINT CK_QuestionOptions_Position CHECK (Position BETWEEN 1 AND 4),
    Text       nvarchar(1000) NOT NULL,
    IsCorrect  bit            NOT NULL,
    RetiredAt  datetime2(7)   NULL,
    CONSTRAINT UQ_QuestionOptions_QuestionId_Position UNIQUE (QuestionId, Position)
);
