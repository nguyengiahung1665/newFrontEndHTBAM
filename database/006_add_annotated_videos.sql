SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID('ManagementAssignments','U') IS NULL
BEGIN
    CREATE TABLE ManagementAssignments(
        Id bigint IDENTITY PRIMARY KEY,
        TeacherId bigint NOT NULL,
        PositionType nvarchar(32) NOT NULL,
        FacultyId bigint NULL,
        DepartmentId bigint NULL,
        EffectiveFrom datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        EffectiveTo datetime2 NULL,
        IsActive bit NOT NULL DEFAULT 1,
        AssignedByUserId bigint NULL,
        AssignedByTeacherId bigint NULL,
        Note nvarchar(max) NULL,
        CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Mgmt_Teacher FOREIGN KEY(TeacherId) REFERENCES Teachers(Id),
        CONSTRAINT FK_Mgmt_Faculty FOREIGN KEY(FacultyId) REFERENCES Faculties(Id),
        CONSTRAINT FK_Mgmt_Department FOREIGN KEY(DepartmentId) REFERENCES Departments(Id),
        CONSTRAINT FK_Mgmt_AssignedByUser FOREIGN KEY(AssignedByUserId) REFERENCES Users(Id),
        CONSTRAINT FK_Mgmt_AssignedByTeacher FOREIGN KEY(AssignedByTeacherId) REFERENCES Teachers(Id),
        CONSTRAINT CK_Mgmt_Position CHECK(PositionType IN('FACULTY_HEAD','DEPARTMENT_HEAD')),
        CONSTRAINT CK_Mgmt_Scope CHECK((PositionType='FACULTY_HEAD' AND FacultyId IS NOT NULL AND DepartmentId IS NULL) OR (PositionType='DEPARTMENT_HEAD' AND DepartmentId IS NOT NULL))
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Mgmt_Active_FacultyHead' AND object_id=OBJECT_ID('ManagementAssignments'))
    EXEC('CREATE UNIQUE INDEX UX_Mgmt_Active_FacultyHead ON ManagementAssignments(FacultyId) WHERE IsActive=1 AND PositionType=''FACULTY_HEAD'' AND FacultyId IS NOT NULL');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Mgmt_Active_DepartmentHead' AND object_id=OBJECT_ID('ManagementAssignments'))
    EXEC('CREATE UNIQUE INDEX UX_Mgmt_Active_DepartmentHead ON ManagementAssignments(DepartmentId) WHERE IsActive=1 AND PositionType=''DEPARTMENT_HEAD'' AND DepartmentId IS NOT NULL');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Mgmt_Teacher_Active' AND object_id=OBJECT_ID('ManagementAssignments'))
    EXEC('CREATE INDEX IX_Mgmt_Teacher_Active ON ManagementAssignments(TeacherId,IsActive,PositionType)');

IF COL_LENGTH('Sessions','OriginalTeacherId') IS NULL
BEGIN
    ALTER TABLE Sessions ADD OriginalTeacherId bigint NULL;
    EXEC('UPDATE s SET OriginalTeacherId=cs.TeacherId FROM Sessions s INNER JOIN ClassSections cs ON cs.Id=s.ClassSectionId WHERE s.OriginalTeacherId IS NULL');
    DECLARE @MissingOriginalTeacher int;
    EXEC sp_executesql N'SELECT @Missing=COUNT(*) FROM Sessions WHERE OriginalTeacherId IS NULL', N'@Missing int OUTPUT', @Missing=@MissingOriginalTeacher OUTPUT;
    IF @MissingOriginalTeacher > 0
        THROW 50001, 'Không thể xác định giảng viên gốc cho một số Session.', 1;
    EXEC('ALTER TABLE Sessions ALTER COLUMN OriginalTeacherId bigint NOT NULL');
END;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Session_OriginalTeacher')
    EXEC('ALTER TABLE Sessions ADD CONSTRAINT FK_Session_OriginalTeacher FOREIGN KEY(OriginalTeacherId) REFERENCES Teachers(Id)');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sessions_OriginalTeacher_Time' AND object_id=OBJECT_ID('Sessions'))
    EXEC('CREATE INDEX IX_Sessions_OriginalTeacher_Time ON Sessions(OriginalTeacherId,ScheduledStart DESC)');

IF OBJECT_ID('SessionSubstitutions','U') IS NULL
BEGIN
    CREATE TABLE SessionSubstitutions(
        Id bigint IDENTITY PRIMARY KEY,
        SessionId bigint NOT NULL,
        OriginalTeacherId bigint NOT NULL,
        SubstituteTeacherId bigint NOT NULL,
        AssignedByUserId bigint NULL,
        AssignedByTeacherId bigint NULL,
        Reason nvarchar(512) NOT NULL,
        Status nvarchar(32) NOT NULL DEFAULT 'ACTIVE',
        AssignedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CancelledAt datetime2 NULL,
        CompletedAt datetime2 NULL,
        Note nvarchar(max) NULL,
        CONSTRAINT FK_Sub_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id),
        CONSTRAINT FK_Sub_OriginalTeacher FOREIGN KEY(OriginalTeacherId) REFERENCES Teachers(Id),
        CONSTRAINT FK_Sub_SubstituteTeacher FOREIGN KEY(SubstituteTeacherId) REFERENCES Teachers(Id),
        CONSTRAINT FK_Sub_AssignedByUser FOREIGN KEY(AssignedByUserId) REFERENCES Users(Id),
        CONSTRAINT FK_Sub_AssignedByTeacher FOREIGN KEY(AssignedByTeacherId) REFERENCES Teachers(Id),
        CONSTRAINT CK_Sub_Status CHECK(Status IN('ACTIVE','CANCELLED','COMPLETED')),
        CONSTRAINT CK_Sub_DifferentTeacher CHECK(OriginalTeacherId<>SubstituteTeacherId)
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Sub_Active_Session' AND object_id=OBJECT_ID('SessionSubstitutions'))
    EXEC('CREATE UNIQUE INDEX UX_Sub_Active_Session ON SessionSubstitutions(SessionId) WHERE Status=''ACTIVE''');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Substitute_Teacher_Status' AND object_id=OBJECT_ID('SessionSubstitutions'))
    EXEC('CREATE INDEX IX_Substitute_Teacher_Status ON SessionSubstitutions(SubstituteTeacherId,Status,AssignedAt DESC)');

IF COL_LENGTH('Videos','VideoType') IS NULL
    ALTER TABLE Videos ADD VideoType nvarchar(32) NOT NULL CONSTRAINT DF_Videos_VideoType DEFAULT 'INPUT_UPLOAD';
IF COL_LENGTH('Videos','SessionId') IS NULL
    ALTER TABLE Videos ADD SessionId bigint NULL;
IF COL_LENGTH('Videos','ParentVideoId') IS NULL
    ALTER TABLE Videos ADD ParentVideoId bigint NULL;
IF COL_LENGTH('Videos','IsSystemGenerated') IS NULL
    ALTER TABLE Videos ADD IsSystemGenerated bit NOT NULL CONSTRAINT DF_Videos_SystemGenerated DEFAULT 0;
IF OBJECT_ID('SessionSubstitutions','U') IS NOT NULL AND COL_LENGTH('SessionSubstitutions','CompletedAt') IS NULL
    ALTER TABLE SessionSubstitutions ADD CompletedAt datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Video_Session')
    EXEC('ALTER TABLE Videos ADD CONSTRAINT FK_Video_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id)');
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Video_Parent')
    EXEC('ALTER TABLE Videos ADD CONSTRAINT FK_Video_Parent FOREIGN KEY(ParentVideoId) REFERENCES Videos(Id)');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Video_Type')
    EXEC('ALTER TABLE Videos ADD CONSTRAINT CK_Video_Type CHECK(VideoType IN(''INPUT_UPLOAD'',''ANNOTATED_OUTPUT''))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_Video_Classification')
    EXEC('ALTER TABLE Videos ADD CONSTRAINT CK_Video_Classification CHECK((VideoType=''INPUT_UPLOAD'' AND IsSystemGenerated=0 AND SessionId IS NULL AND ParentVideoId IS NULL) OR (VideoType=''ANNOTATED_OUTPUT'' AND IsSystemGenerated=1 AND SessionId IS NOT NULL))');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Videos_Annotated_Session' AND object_id=OBJECT_ID('Videos'))
    EXEC('CREATE UNIQUE INDEX UX_Videos_Annotated_Session ON Videos(SessionId,VideoType) WHERE VideoType=''ANNOTATED_OUTPUT'' AND SessionId IS NOT NULL AND Status<>''DELETED''');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Videos_Type_Session' AND object_id=OBJECT_ID('Videos'))
    EXEC('CREATE INDEX IX_Videos_Type_Session ON Videos(VideoType,SessionId,Status)');

COMMIT TRANSACTION;
