-- Each Participant's Recap is at /recap/<RecapToken>: 256 random bits as 64 lowercase hex characters,
-- drawn by SQL Server's cryptographic generator. It is a separate secret from the Participant's cookie
-- token, so a shared Recap link can be read but never used to answer as the Participant. It is stored as
-- is (not hashed) so the Host's results page can link to any Participant's Recap.
ALTER TABLE dbo.Participants
    ADD RecapToken char(64) NULL;
GO

-- Existing Participants get theirs one row at a time, so each draw is its own.
DECLARE @Id int = (SELECT MIN(Id) FROM dbo.Participants);
WHILE @Id IS NOT NULL
BEGIN
    UPDATE dbo.Participants SET RecapToken = LOWER(CONVERT(char(64), CRYPT_GEN_RANDOM(32), 2)) WHERE Id = @Id;
    SET @Id = (SELECT MIN(Id) FROM dbo.Participants WHERE Id > @Id);
END
GO

ALTER TABLE dbo.Participants ALTER COLUMN RecapToken char(64) NOT NULL;

ALTER TABLE dbo.Participants
    ADD CONSTRAINT DF_Participants_RecapToken DEFAULT (LOWER(CONVERT(char(64), CRYPT_GEN_RANDOM(32), 2))) FOR RecapToken,
        CONSTRAINT UQ_Participants_RecapToken UNIQUE (RecapToken);
