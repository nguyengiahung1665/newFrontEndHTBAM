USE HTBAM;
GO

IF OBJECT_ID('dbo.Faculties','U') IS NOT NULL AND COL_LENGTH('dbo.Faculties','Note') IS NULL ALTER TABLE dbo.Faculties ADD Note nvarchar(max) NULL;
IF OBJECT_ID('dbo.Departments','U') IS NOT NULL AND COL_LENGTH('dbo.Departments','Note') IS NULL ALTER TABLE dbo.Departments ADD Note nvarchar(max) NULL;
IF OBJECT_ID('dbo.StudentClasses','U') IS NOT NULL AND COL_LENGTH('dbo.StudentClasses','Note') IS NULL ALTER TABLE dbo.StudentClasses ADD Note nvarchar(max) NULL;
IF OBJECT_ID('dbo.Courses','U') IS NOT NULL AND COL_LENGTH('dbo.Courses','Note') IS NULL ALTER TABLE dbo.Courses ADD Note nvarchar(max) NULL;
IF OBJECT_ID('dbo.Teachers','U') IS NOT NULL AND COL_LENGTH('dbo.Teachers','Note') IS NULL ALTER TABLE dbo.Teachers ADD Note nvarchar(max) NULL;
IF OBJECT_ID('dbo.ClassSections','U') IS NOT NULL AND COL_LENGTH('dbo.ClassSections','Note') IS NULL ALTER TABLE dbo.ClassSections ADD Note nvarchar(max) NULL;
IF OBJECT_ID('dbo.Rooms','U') IS NOT NULL AND COL_LENGTH('dbo.Rooms','Note') IS NULL ALTER TABLE dbo.Rooms ADD Note nvarchar(max) NULL;
IF OBJECT_ID('dbo.Cameras','U') IS NOT NULL AND COL_LENGTH('dbo.Cameras','Note') IS NULL ALTER TABLE dbo.Cameras ADD Note nvarchar(max) NULL;
GO
