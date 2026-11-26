-- A browser joins an Open Masterclass with its Join code. Takes time only from @Now and randomness
-- only from @Seed (same seed, same Nickname drawn from the same free ones).
--
-- @CurrentToken is the token the browser already holds, if any. If it is a Participant of this
-- Masterclass, that Participant is returned as is. Otherwise a new Participant is created for
-- @NewToken, with a Nickname (adjective + animal) that nobody in the Masterclass has yet.
--
-- Returns one row (the Participant and its token), or no row when the code belongs to no Open
-- Masterclass. Tokens are stored only as their SHA-256 hash.
CREATE OR ALTER PROCEDURE dbo.JoinMasterclass
    @JoinCode     nvarchar(50),
    @CurrentToken varchar(200) = NULL,
    @NewToken     varchar(200),
    @Seed         int,
    @Now          datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Locking the Masterclass row makes its joins take turns, so two can't draw the same Nickname.
    DECLARE @MasterclassId int = (
        SELECT Id FROM dbo.Masterclasses WITH (UPDLOCK, HOLDLOCK)
        WHERE Status = 2 AND JoinCode = TRIM(@JoinCode));
    IF @MasterclassId IS NULL
    BEGIN
        COMMIT;
        RETURN;
    END

    DECLARE @Token varchar(200) = @CurrentToken;
    DECLARE @ParticipantId int = (
        SELECT Id FROM dbo.Participants
        WHERE MasterclassId = @MasterclassId AND TokenHash = HASHBYTES('SHA2_256', @CurrentToken));

    IF @ParticipantId IS NULL
    BEGIN
        DECLARE @Adjectives TABLE (Word nvarchar(20) PRIMARY KEY);
        INSERT @Adjectives VALUES
            (N'Brave'), (N'Bright'), (N'Calm'), (N'Clever'), (N'Cosy'), (N'Curious'), (N'Daring'), (N'Eager'),
            (N'Friendly'), (N'Gentle'), (N'Happy'), (N'Honest'), (N'Jolly'), (N'Keen'), (N'Kind'), (N'Lively'),
            (N'Lucky'), (N'Merry'), (N'Mighty'), (N'Nimble'), (N'Noble'), (N'Patient'), (N'Plucky'), (N'Polite'),
            (N'Proud'), (N'Quick'), (N'Quiet'), (N'Rapid'), (N'Sharp'), (N'Shiny'), (N'Smart'), (N'Snappy'),
            (N'Speedy'), (N'Steady'), (N'Sunny'), (N'Swift'), (N'Tidy'), (N'Wise'), (N'Witty'), (N'Zesty');
        DECLARE @Animals TABLE (Word nvarchar(20) PRIMARY KEY);
        INSERT @Animals VALUES
            (N'Badger'), (N'Beaver'), (N'Bison'), (N'Camel'), (N'Cheetah'), (N'Crane'), (N'Dolphin'), (N'Eagle'),
            (N'Falcon'), (N'Ferret'), (N'Fox'), (N'Gecko'), (N'Giraffe'), (N'Hare'), (N'Hedgehog'), (N'Heron'),
            (N'Koala'), (N'Lemur'), (N'Lion'), (N'Lynx'), (N'Marmot'), (N'Meerkat'), (N'Moose'), (N'Otter'),
            (N'Owl'), (N'Panda'), (N'Parrot'), (N'Pelican'), (N'Penguin'), (N'Puffin'), (N'Rabbit'), (N'Raven'),
            (N'Seal'), (N'Sparrow'), (N'Squirrel'), (N'Tiger'), (N'Toucan'), (N'Turtle'), (N'Walrus'), (N'Zebra');

        -- Draw one of the free Nicknames, each equally likely.
        DECLARE @Free TABLE (Position int IDENTITY PRIMARY KEY, Nickname nvarchar(100) NOT NULL);
        INSERT @Free (Nickname)
        SELECT a.Word + N' ' + n.Word
        FROM @Adjectives a CROSS JOIN @Animals n
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.Participants p
            WHERE p.MasterclassId = @MasterclassId AND p.Nickname = a.Word + N' ' + n.Word)
        ORDER BY a.Word, n.Word;

        DECLARE @FreeCount int = @@ROWCOUNT;
        DECLARE @Draw float = RAND(@Seed);
        DECLARE @Nickname nvarchar(100);
        IF @FreeCount > 0
            SELECT @Nickname = Nickname FROM @Free WHERE Position = 1 + FLOOR(@Draw * @FreeCount);
        ELSE
        BEGIN
            -- Every combination is taken: number the next one. The count only grows, so the number is new.
            DECLARE @Joined int = (SELECT COUNT(*) FROM dbo.Participants WHERE MasterclassId = @MasterclassId);
            DECLARE @Combinations int = (SELECT COUNT(*) FROM @Adjectives) * (SELECT COUNT(*) FROM @Animals);
            SELECT @Nickname = Nickname + N' ' + CAST(@Joined + 1 AS nvarchar(10))
            FROM (
                SELECT a.Word + N' ' + n.Word AS Nickname, ROW_NUMBER() OVER (ORDER BY a.Word, n.Word) AS Position
                FROM @Adjectives a CROSS JOIN @Animals n) c
            WHERE c.Position = 1 + FLOOR(@Draw * @Combinations);
        END

        INSERT dbo.Participants (MasterclassId, TokenHash, Nickname, JoinedAt)
        VALUES (@MasterclassId, HASHBYTES('SHA2_256', @NewToken), @Nickname, @Now);
        SET @ParticipantId = SCOPE_IDENTITY();
        SET @Token = @NewToken;
    END

    COMMIT;

    SELECT @Token AS Token, p.Id, p.MasterclassId, m.Name AS MasterclassName, CAST(m.Status AS int) AS MasterclassStatus,
           p.Nickname, p.JoinedAt
    FROM dbo.Participants p
    JOIN dbo.Masterclasses m ON m.Id = p.MasterclassId
    WHERE p.Id = @ParticipantId;
END
