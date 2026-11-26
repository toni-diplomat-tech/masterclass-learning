-- Only one Masterclass can be Open at a time (dbo.OpenMasterclass refuses the rest). This index makes
-- the database guarantee it too: a second row with Status 2 (Open) fails to insert or update.
-- It changes no data, so it fails on a database that already has several Open Masterclasses: close all
-- but one of them first (the Host's Close Masterclass, or dbo.CloseMasterclass).
--
-- With at most one Open row, its Join code is unique among Open Masterclasses on its own, so the
-- older UQ_Masterclasses_JoinCode_Open is redundant and is dropped: one index, one rule.

IF (SELECT COUNT(*) FROM dbo.Masterclasses WHERE Status = 2) > 1
    THROW 50001, N'More than one Masterclass is Open. Close all but one, then run the migration again.', 1;

CREATE UNIQUE INDEX UQ_Masterclasses_OneOpen ON dbo.Masterclasses (Status) WHERE Status = 2;

DROP INDEX UQ_Masterclasses_JoinCode_Open ON dbo.Masterclasses;
