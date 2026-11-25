-- The points each Answer scored, decided when it is submitted (dbo.SubmitAnswer) from the Question's
-- Difficulty and time limit as they were then, so re-importing a Question never changes past scores.
-- Only a Correct Answer scores; Incorrect and Timed out score 0.
ALTER TABLE dbo.Answers
    ADD Points int NOT NULL CONSTRAINT DF_Answers_Points DEFAULT 0,
        CONSTRAINT CK_Answers_Points CHECK (Points >= 0 AND (Outcome = 1 OR Points = 0));
