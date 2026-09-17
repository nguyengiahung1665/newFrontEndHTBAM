SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.ClassSections', 'AutoComment') IS NULL
    ALTER TABLE dbo.ClassSections ADD AutoComment nvarchar(max) NULL;
IF COL_LENGTH('dbo.ClassSections', 'AutoCommentProvider') IS NULL
    ALTER TABLE dbo.ClassSections ADD AutoCommentProvider nvarchar(64) NULL;
IF COL_LENGTH('dbo.ClassSections', 'AutoCommentGeneratedAt') IS NULL
    ALTER TABLE dbo.ClassSections ADD AutoCommentGeneratedAt datetime2 NULL;

IF COL_LENGTH('dbo.StudentSessionSummaries', 'AutoCommentGeneratedAt') IS NULL
    ALTER TABLE dbo.StudentSessionSummaries ADD AutoCommentGeneratedAt datetime2 NULL;
IF COL_LENGTH('dbo.ClassSessionSummaries', 'AutoCommentGeneratedAt') IS NULL
    ALTER TABLE dbo.ClassSessionSummaries ADD AutoCommentGeneratedAt datetime2 NULL;

COMMIT TRANSACTION;
GO

