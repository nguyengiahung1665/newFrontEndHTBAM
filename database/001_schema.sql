/* HTBAM Web System v3 - Fresh schema for SQL Server
   If you have never run v1/v2, run 001 -> 002 -> 003(optional). Do NOT run legacy/004.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO

IF OBJECT_ID('dbo.Roles','U') IS NOT NULL THROW 50001, 'Database is not empty. Use a clean HTBAM database for 001_schema.sql.', 1;
GO

CREATE TABLE Roles(
 Id bigint IDENTITY PRIMARY KEY,
 Name nvarchar(64) NOT NULL UNIQUE
);
CREATE TABLE Users(
 Id bigint IDENTITY PRIMARY KEY,
 UserName nvarchar(128) NOT NULL UNIQUE,
 Email nvarchar(256) NOT NULL DEFAULT '',
 PasswordHash nvarchar(512) NOT NULL,
 FullName nvarchar(256) NOT NULL,
 Status nvarchar(32) NOT NULL DEFAULT 'ACTIVE',
 TokenVersion int NOT NULL DEFAULT 1,
 CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 LastLoginAt datetime2 NULL,
 CONSTRAINT CK_Users_Status CHECK(Status IN('ACTIVE','LOCKED','INACTIVE'))
);
CREATE UNIQUE INDEX UX_Users_Email_NotEmpty ON Users(Email) WHERE Email <> '';
CREATE TABLE UserRoles(
 UserId bigint NOT NULL,
 RoleId bigint NOT NULL,
 CONSTRAINT PK_UserRoles PRIMARY KEY(UserId,RoleId),
 CONSTRAINT FK_UserRoles_User FOREIGN KEY(UserId) REFERENCES Users(Id),
 CONSTRAINT FK_UserRoles_Role FOREIGN KEY(RoleId) REFERENCES Roles(Id)
);

CREATE TABLE Faculties(
 Id bigint IDENTITY PRIMARY KEY,
 Code nvarchar(64) NOT NULL UNIQUE,
 Name nvarchar(256) NOT NULL,
 Note nvarchar(max) NULL,
 IsActive bit NOT NULL DEFAULT 1
);
CREATE TABLE Departments(
 Id bigint IDENTITY PRIMARY KEY,
 Code nvarchar(64) NOT NULL UNIQUE,
 Name nvarchar(256) NOT NULL,
 FacultyId bigint NOT NULL,
 Note nvarchar(max) NULL,
 IsActive bit NOT NULL DEFAULT 1,
 CONSTRAINT FK_Department_Faculty FOREIGN KEY(FacultyId) REFERENCES Faculties(Id)
);
CREATE TABLE StudentClasses(
 Id bigint IDENTITY PRIMARY KEY,
 Code nvarchar(64) NOT NULL UNIQUE,
 Name nvarchar(256) NOT NULL,
 FacultyId bigint NOT NULL,
 StartYear int NULL,
 Note nvarchar(max) NULL,
 IsActive bit NOT NULL DEFAULT 1,
 CONSTRAINT FK_StudentClass_Faculty FOREIGN KEY(FacultyId) REFERENCES Faculties(Id)
);
CREATE TABLE Students(
 Id bigint IDENTITY PRIMARY KEY,
 StudentCode nvarchar(64) NOT NULL UNIQUE,
 FullName nvarchar(256) NOT NULL,
 Email nvarchar(256) NOT NULL DEFAULT '',
 StudentClassId bigint NULL,
 AnonymousCode nvarchar(128) NOT NULL UNIQUE,
 IsActive bit NOT NULL DEFAULT 1,
 CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_Student_Class FOREIGN KEY(StudentClassId) REFERENCES StudentClasses(Id)
);
CREATE UNIQUE INDEX UX_Students_Email_NotEmpty ON Students(Email) WHERE Email <> '';
CREATE TABLE Teachers(
 Id bigint IDENTITY PRIMARY KEY,
 TeacherCode nvarchar(64) NOT NULL UNIQUE,
 FullName nvarchar(256) NOT NULL,
 Email nvarchar(256) NOT NULL DEFAULT '',
 UserId bigint NULL,
 DepartmentId bigint NULL,
 Note nvarchar(max) NULL,
 IsActive bit NOT NULL DEFAULT 1,
 CONSTRAINT FK_Teacher_User FOREIGN KEY(UserId) REFERENCES Users(Id),
 CONSTRAINT FK_Teacher_Department FOREIGN KEY(DepartmentId) REFERENCES Departments(Id)
);
CREATE UNIQUE INDEX UX_Teacher_User_NotNull ON Teachers(UserId) WHERE UserId IS NOT NULL;
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
CREATE UNIQUE INDEX UX_Mgmt_Active_FacultyHead ON ManagementAssignments(FacultyId) WHERE IsActive = 1 AND PositionType = 'FACULTY_HEAD' AND FacultyId IS NOT NULL;
CREATE UNIQUE INDEX UX_Mgmt_Active_DepartmentHead ON ManagementAssignments(DepartmentId) WHERE IsActive = 1 AND PositionType = 'DEPARTMENT_HEAD' AND DepartmentId IS NOT NULL;
CREATE TABLE Courses(
 Id bigint IDENTITY PRIMARY KEY,
 Code nvarchar(64) NOT NULL UNIQUE,
 Name nvarchar(256) NOT NULL,
 DepartmentId bigint NULL,
 Credits int NOT NULL DEFAULT 3,
 Note nvarchar(max) NULL,
 IsActive bit NOT NULL DEFAULT 1,
 CONSTRAINT FK_Course_Department FOREIGN KEY(DepartmentId) REFERENCES Departments(Id),
 CONSTRAINT CK_Course_Credits CHECK(Credits BETWEEN 1 AND 20)
);
CREATE TABLE ClassSections(
 Id bigint IDENTITY PRIMARY KEY,
 Code nvarchar(64) NOT NULL UNIQUE,
 Name nvarchar(256) NOT NULL,
 CourseId bigint NOT NULL,
 TeacherId bigint NOT NULL,
 Semester nvarchar(64) NOT NULL,
 AcademicYear nvarchar(32) NOT NULL DEFAULT '',
 Note nvarchar(max) NULL,
 IsActive bit NOT NULL DEFAULT 1,
 AutoComment nvarchar(max) NULL,
 AutoCommentProvider nvarchar(64) NULL,
 AutoCommentGeneratedAt datetime2 NULL,
 CONSTRAINT FK_ClassSection_Course FOREIGN KEY(CourseId) REFERENCES Courses(Id),
 CONSTRAINT FK_ClassSection_Teacher FOREIGN KEY(TeacherId) REFERENCES Teachers(Id)
);
CREATE TABLE Enrollments(
 ClassSectionId bigint NOT NULL,
 StudentId bigint NOT NULL,
 EnrolledAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT PK_Enrollments PRIMARY KEY(ClassSectionId,StudentId),
 CONSTRAINT FK_Enrollment_Class FOREIGN KEY(ClassSectionId) REFERENCES ClassSections(Id),
 CONSTRAINT FK_Enrollment_Student FOREIGN KEY(StudentId) REFERENCES Students(Id)
);
CREATE TABLE Rooms(
 Id bigint IDENTITY PRIMARY KEY,
 Code nvarchar(64) NOT NULL UNIQUE,
 Name nvarchar(256) NOT NULL,
 Location nvarchar(256) NULL,
 Capacity int NOT NULL DEFAULT 0,
 Note nvarchar(max) NULL,
 IsActive bit NOT NULL DEFAULT 1,
 CONSTRAINT CK_Room_Capacity CHECK(Capacity >= 0)
);
CREATE TABLE Cameras(
 Id bigint IDENTITY PRIMARY KEY,
 Code nvarchar(64) NOT NULL UNIQUE,
 Name nvarchar(256) NOT NULL DEFAULT '',
 RoomId bigint NOT NULL,
 RtspUrl nvarchar(1024) NOT NULL,
 Status nvarchar(32) NOT NULL DEFAULT 'OFFLINE',
 LastHealthCheckAt datetime2 NULL,
 LastHealthMessage nvarchar(1000) NULL,
 Note nvarchar(max) NULL,
 IsActive bit NOT NULL DEFAULT 1,
 CONSTRAINT FK_Camera_Room FOREIGN KEY(RoomId) REFERENCES Rooms(Id),
 CONSTRAINT CK_Camera_Status CHECK(Status IN('ONLINE','OFFLINE','ERROR','UNKNOWN'))
);

CREATE TABLE FaceEnrollments(
 Id bigint IDENTITY PRIMARY KEY,
 StudentId bigint NOT NULL,
 Status nvarchar(32) NOT NULL DEFAULT 'IN_PROGRESS',
 RequiredPoses nvarchar(128) NOT NULL DEFAULT 'FRONT,LEFT,RIGHT',
 MinAcceptedImages int NOT NULL DEFAULT 6,
 AcceptedImageCount int NOT NULL DEFAULT 0,
 FailureReason nvarchar(1000) NULL,
 StartedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 SubmittedAt datetime2 NULL,
 CompletedAt datetime2 NULL,
 CONSTRAINT FK_FaceEnrollment_Student FOREIGN KEY(StudentId) REFERENCES Students(Id),
 CONSTRAINT CK_FaceEnrollment_Status CHECK(Status IN('IN_PROGRESS','NEEDS_RETAKE','PENDING_AI','PROCESSING','READY','FAILED','CANCELLED'))
);
CREATE TABLE StudentFaceImages(
 Id bigint IDENTITY PRIMARY KEY,
 FaceEnrollmentId bigint NOT NULL,
 StudentId bigint NOT NULL,
 ObjectKey nvarchar(512) NOT NULL UNIQUE,
 OriginalFileName nvarchar(256) NOT NULL,
 ContentType nvarchar(128) NOT NULL,
 SizeBytes bigint NOT NULL,
 Sha256 char(64) NOT NULL,
 CapturePose nvarchar(32) NOT NULL,
 SourceType nvarchar(32) NOT NULL,
 QualityScore decimal(8,5) NULL,
 QualityStatus nvarchar(32) NOT NULL DEFAULT 'PENDING',
 QualityReason nvarchar(1000) NULL,
 IsActive bit NOT NULL DEFAULT 1,
 CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 ProcessedAt datetime2 NULL,
 CONSTRAINT FK_FaceImage_Enrollment FOREIGN KEY(FaceEnrollmentId) REFERENCES FaceEnrollments(Id),
 CONSTRAINT FK_FaceImage_Student FOREIGN KEY(StudentId) REFERENCES Students(Id),
 CONSTRAINT CK_FaceImage_Pose CHECK(CapturePose IN('FRONT','LEFT','RIGHT','UP','DOWN')),
 CONSTRAINT CK_FaceImage_Source CHECK(SourceType IN('UPLOAD','CAMERA')),
 CONSTRAINT CK_FaceImage_Quality CHECK(QualityStatus IN('PENDING','PASS','RETAKE','REJECTED'))
);
CREATE TABLE StudentFaceTemplates(
 Id bigint IDENTITY PRIMARY KEY,
 StudentId bigint NOT NULL,
 FaceEnrollmentId bigint NULL,
 TemplateRef nvarchar(512) NOT NULL,
 QualityScore decimal(8,5) NOT NULL,
 PoseCoverage nvarchar(128) NOT NULL,
 ModelName nvarchar(128) NOT NULL DEFAULT 'ArcFace',
 TemplateVersion nvarchar(64) NOT NULL,
 EmbeddingDimension int NOT NULL DEFAULT 512,
 SourceImageCount int NOT NULL DEFAULT 0,
 QualityStatus nvarchar(32) NOT NULL,
 IsActive bit NOT NULL DEFAULT 1,
 CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_FaceTemplate_Student FOREIGN KEY(StudentId) REFERENCES Students(Id),
 CONSTRAINT FK_FaceTemplate_Enrollment FOREIGN KEY(FaceEnrollmentId) REFERENCES FaceEnrollments(Id)
);

CREATE TABLE Videos(
 Id bigint IDENTITY PRIMARY KEY,
 FileName nvarchar(256) NOT NULL,
 StorageRef nvarchar(512) NOT NULL,
 ContentType nvarchar(128) NOT NULL DEFAULT 'video/mp4',
 SizeBytes bigint NOT NULL,
 Sha256 char(64) NOT NULL,
 Status nvarchar(32) NOT NULL DEFAULT 'READY',
 UploadedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 UploadedByUserId bigint NULL,
 CONSTRAINT FK_Video_Uploader FOREIGN KEY(UploadedByUserId) REFERENCES Users(Id),
 CONSTRAINT CK_Video_Status CHECK(Status IN('READY','DELETED','PROCESSING','FAILED'))
);
CREATE TABLE AttendancePolicies(
 Id bigint IDENTITY PRIMARY KEY,
 Code nvarchar(64) NOT NULL UNIQUE,
 Name nvarchar(256) NOT NULL,
 PresentThreshold decimal(8,5) NOT NULL,
 PartialThreshold decimal(8,5) NOT NULL,
 PresentScore decimal(8,5) NOT NULL,
 PartialScore decimal(8,5) NOT NULL,
 AbsentScore decimal(8,5) NOT NULL,
 Version nvarchar(64) NOT NULL,
 IsActive bit NOT NULL DEFAULT 1,
 CONSTRAINT CK_AttPolicy_Thresholds CHECK(PresentThreshold BETWEEN 0 AND 1 AND PartialThreshold BETWEEN 0 AND 1 AND PresentThreshold >= PartialThreshold)
);
CREATE TABLE Sessions(
 Id bigint IDENTITY PRIMARY KEY,
 ClassSectionId bigint NOT NULL,
 OriginalTeacherId bigint NOT NULL,
 RoomId bigint NULL,
 CameraId bigint NULL,
 VideoId bigint NULL,
 AttendancePolicyId bigint NOT NULL,
 ScheduledStart datetime2 NOT NULL,
 ScheduledEnd datetime2 NULL,
 StartedAt datetime2 NULL,
 EndedAt datetime2 NULL,
 Status nvarchar(32) NOT NULL DEFAULT 'DRAFT',
 AlertProfile nvarchar(64) NOT NULL DEFAULT 'DEFAULT',
 LecturerComment nvarchar(max) NULL,
 CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 RowVersion rowversion,
 CONSTRAINT FK_Session_Class FOREIGN KEY(ClassSectionId) REFERENCES ClassSections(Id),
 CONSTRAINT FK_Session_OriginalTeacher FOREIGN KEY(OriginalTeacherId) REFERENCES Teachers(Id),
 CONSTRAINT FK_Session_Room FOREIGN KEY(RoomId) REFERENCES Rooms(Id),
 CONSTRAINT FK_Session_Camera FOREIGN KEY(CameraId) REFERENCES Cameras(Id),
 CONSTRAINT FK_Session_Video FOREIGN KEY(VideoId) REFERENCES Videos(Id),
 CONSTRAINT FK_Session_AttPolicy FOREIGN KEY(AttendancePolicyId) REFERENCES AttendancePolicies(Id),
 CONSTRAINT CK_Session_Source CHECK((CameraId IS NOT NULL AND VideoId IS NULL) OR (CameraId IS NULL AND VideoId IS NOT NULL)),
 CONSTRAINT CK_Session_Status CHECK(Status IN('DRAFT','READY','STARTING','RUNNING','FINALIZING','FINALIZE_FAILED','COMPLETED','CANCELLED'))
);
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
 Note nvarchar(max) NULL,
 CONSTRAINT FK_Sub_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id),
 CONSTRAINT FK_Sub_OriginalTeacher FOREIGN KEY(OriginalTeacherId) REFERENCES Teachers(Id),
 CONSTRAINT FK_Sub_SubstituteTeacher FOREIGN KEY(SubstituteTeacherId) REFERENCES Teachers(Id),
 CONSTRAINT FK_Sub_AssignedByUser FOREIGN KEY(AssignedByUserId) REFERENCES Users(Id),
 CONSTRAINT FK_Sub_AssignedByTeacher FOREIGN KEY(AssignedByTeacherId) REFERENCES Teachers(Id),
 CONSTRAINT CK_Sub_Status CHECK(Status IN('ACTIVE','CANCELLED','COMPLETED')),
 CONSTRAINT CK_Sub_DifferentTeacher CHECK(OriginalTeacherId <> SubstituteTeacherId)
);
CREATE UNIQUE INDEX UX_Sub_Active_Session ON SessionSubstitutions(SessionId) WHERE Status = 'ACTIVE';
CREATE TABLE SessionStudents(
 SessionId bigint NOT NULL,
 StudentId bigint NOT NULL,
 CONSTRAINT PK_SessionStudents PRIMARY KEY(SessionId,StudentId),
 CONSTRAINT FK_SS_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id),
 CONSTRAINT FK_SS_Student FOREIGN KEY(StudentId) REFERENCES Students(Id)
);
CREATE TABLE AnalysisJobs(
 Id bigint IDENTITY PRIMARY KEY,
 SessionId bigint NOT NULL,
 CorrelationId nvarchar(64) NOT NULL UNIQUE,
 ExternalJobId nvarchar(128) NOT NULL DEFAULT '',
 Status nvarchar(32) NOT NULL,
 ModelVersion nvarchar(128) NOT NULL DEFAULT 'not-loaded',
 CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 StartedAt datetime2 NULL,
 EndedAt datetime2 NULL,
 ErrorMessage nvarchar(1000) NULL,
 CONSTRAINT FK_Job_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id)
);

CREATE TABLE StableIdentities(
 Id bigint IDENTITY PRIMARY KEY,
 SessionId bigint NOT NULL,
 StableId nvarchar(128) NOT NULL,
 StudentId bigint NULL,
 CurrentTrackId nvarchar(128) NULL,
 IdentityStatus nvarchar(32) NOT NULL DEFAULT 'UNKNOWN',
 IdentityConfidence decimal(8,5) NULL,
 IdentityMargin decimal(8,5) NULL,
 FirstSeenAt datetime2 NOT NULL,
 LastSeenAt datetime2 NOT NULL,
 State nvarchar(32) NOT NULL DEFAULT 'ACTIVE',
 LastZone nvarchar(128) NULL,
 SeatHint nvarchar(128) NULL,
 ModelVersion nvarchar(128) NOT NULL DEFAULT '',
 CONSTRAINT UQ_Stable UNIQUE(SessionId,StableId),
 CONSTRAINT FK_Stable_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id),
 CONSTRAINT FK_Stable_Student FOREIGN KEY(StudentId) REFERENCES Students(Id),
 CONSTRAINT CK_Stable_State CHECK(State IN('ACTIVE','LOST','CLOSED')),
 CONSTRAINT CK_Stable_IdentityStatus CHECK(IdentityStatus IN('UNKNOWN','IDENTIFIED','CONFLICT'))
);
CREATE TABLE TrackSegments(
 Id bigint IDENTITY PRIMARY KEY,
 StableIdentityId bigint NOT NULL,
 TrackId nvarchar(128) NOT NULL,
 StartedAt datetime2 NOT NULL,
 EndedAt datetime2 NOT NULL,
 AvgQuality decimal(8,5) NULL,
 ObservationCount int NOT NULL DEFAULT 1,
 CONSTRAINT FK_Track_Stable FOREIGN KEY(StableIdentityId) REFERENCES StableIdentities(Id)
);
CREATE TABLE IdentityLinks(
 Id bigint IDENTITY PRIMARY KEY,
 StableIdentityId bigint NOT NULL,
 StudentId bigint NULL,
 EvidenceType nvarchar(64) NOT NULL,
 Score decimal(8,5) NOT NULL,
 Margin decimal(8,5) NULL,
 Decision nvarchar(32) NOT NULL DEFAULT 'EVIDENCE',
 ModelVersion nvarchar(128) NOT NULL,
 CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_IdentityLink_Stable FOREIGN KEY(StableIdentityId) REFERENCES StableIdentities(Id),
 CONSTRAINT FK_IdentityLink_Student FOREIGN KEY(StudentId) REFERENCES Students(Id)
);
CREATE TABLE AiEventReceipts(
 Id bigint IDENTITY PRIMARY KEY,
 ExternalEventId nvarchar(128) NOT NULL UNIQUE,
 SessionId bigint NOT NULL,
 EventType nvarchar(64) NOT NULL,
 EventTimestamp datetime2 NOT NULL,
 ReceivedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_AiReceipt_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id)
);
CREATE TABLE BehaviorEvents(
 Id bigint IDENTITY PRIMARY KEY,
 ExternalEventId nvarchar(128) NOT NULL UNIQUE,
 SessionId bigint NOT NULL,
 StableIdentityId bigint NOT NULL,
 StudentId bigint NULL,
 Label nvarchar(32) NOT NULL,
 Probability decimal(8,5) NOT NULL,
 StartedAt datetime2 NOT NULL,
 EndedAt datetime2 NOT NULL,
 ObservationQuality decimal(8,5) NOT NULL,
 ModelVersion nvarchar(128) NOT NULL,
 ThresholdVersion nvarchar(128) NOT NULL,
 CONSTRAINT FK_BE_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id),
 CONSTRAINT FK_BE_Stable FOREIGN KEY(StableIdentityId) REFERENCES StableIdentities(Id),
 CONSTRAINT FK_BE_Student FOREIGN KEY(StudentId) REFERENCES Students(Id),
 CONSTRAINT CK_BE_Label CHECK(Label IN('FOCUSED','DISTRACTED','SLEEPY','ACTIVE','PHONE_USE','OUT_OF_VIEW'))
);
CREATE TABLE AlertRules(
 Id bigint IDENTITY PRIMARY KEY,
 Code nvarchar(64) NOT NULL UNIQUE,
 Name nvarchar(256) NOT NULL DEFAULT '',
 BehaviorLabel nvarchar(32) NOT NULL,
 MinDurationSeconds int NOT NULL,
 MinConfidence decimal(8,5) NOT NULL,
 MinObservationQuality decimal(8,5) NOT NULL DEFAULT 0.5,
 Enabled bit NOT NULL DEFAULT 1,
 Version nvarchar(32) NOT NULL
);
CREATE TABLE Alerts(
 Id bigint IDENTITY PRIMARY KEY,
 ExternalEventId nvarchar(128) NOT NULL UNIQUE,
 SessionId bigint NOT NULL,
 StableIdentityId bigint NOT NULL,
 StudentId bigint NULL,
 Type nvarchar(64) NOT NULL,
 StartedAt datetime2 NOT NULL,
 EndedAt datetime2 NULL,
 DurationSeconds int NOT NULL,
 Confidence decimal(8,5) NOT NULL,
 ObservationQuality decimal(8,5) NOT NULL,
 Status nvarchar(32) NOT NULL DEFAULT 'OPEN',
 LecturerNote nvarchar(1000) NULL,
 EvidenceRef nvarchar(512) NULL,
 CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 AcknowledgedAt datetime2 NULL,
 ClosedAt datetime2 NULL,
 CONSTRAINT FK_Alert_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id),
 CONSTRAINT FK_Alert_Stable FOREIGN KEY(StableIdentityId) REFERENCES StableIdentities(Id),
 CONSTRAINT FK_Alert_Student FOREIGN KEY(StudentId) REFERENCES Students(Id),
 CONSTRAINT CK_Alert_Status CHECK(Status IN('OPEN','ACK','CLOSED'))
);
CREATE TABLE Attendance(
 Id bigint IDENTITY PRIMARY KEY,
 SessionId bigint NOT NULL,
 StudentId bigint NOT NULL,
 PresentSeconds int NOT NULL DEFAULT 0,
 ObservedSeconds int NOT NULL DEFAULT 0,
 PresenceRatio decimal(8,5) NOT NULL DEFAULT 0,
 ObservationQuality decimal(8,5) NOT NULL DEFAULT 0,
 Status nvarchar(32) NOT NULL DEFAULT 'UNKNOWN',
 Score decimal(8,5) NOT NULL DEFAULT 0,
 PolicyVersion nvarchar(64) NOT NULL DEFAULT '',
 CONSTRAINT UQ_Attendance UNIQUE(SessionId,StudentId),
 CONSTRAINT FK_Att_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id),
 CONSTRAINT FK_Att_Student FOREIGN KEY(StudentId) REFERENCES Students(Id)
);
CREATE TABLE StudentSessionSummaries(
 Id bigint IDENTITY PRIMARY KEY,
 SessionId bigint NOT NULL,
 StudentId bigint NOT NULL,
 FocusedRatio decimal(8,5) NOT NULL DEFAULT 0,
 DistractedRatio decimal(8,5) NOT NULL DEFAULT 0,
 SleepyRatio decimal(8,5) NOT NULL DEFAULT 0,
 ActiveRatio decimal(8,5) NOT NULL DEFAULT 0,
 AlertCount int NOT NULL DEFAULT 0,
 AlertDurationSeconds int NOT NULL DEFAULT 0,
 ObservationQuality decimal(8,5) NOT NULL DEFAULT 0,
 AutoComment nvarchar(max) NULL,
 AutoCommentProvider nvarchar(64) NULL,
 AutoCommentGeneratedAt datetime2 NULL,
 CONSTRAINT UQ_StudentSummary UNIQUE(SessionId,StudentId),
 CONSTRAINT FK_SSum_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id),
 CONSTRAINT FK_SSum_Student FOREIGN KEY(StudentId) REFERENCES Students(Id)
);
CREATE TABLE ClassSessionSummaries(
 Id bigint IDENTITY PRIMARY KEY,
 SessionId bigint NOT NULL UNIQUE,
 ObservedStudentCount int NOT NULL DEFAULT 0,
 FocusedRatio decimal(8,5) NOT NULL DEFAULT 0,
 DistractedRatio decimal(8,5) NOT NULL DEFAULT 0,
 SleepyRatio decimal(8,5) NOT NULL DEFAULT 0,
 ActiveRatio decimal(8,5) NOT NULL DEFAULT 0,
 AlertCount int NOT NULL DEFAULT 0,
 CameraAiQuality decimal(8,5) NOT NULL DEFAULT 0,
 AutoComment nvarchar(max) NULL,
 AutoCommentProvider nvarchar(64) NULL,
 AutoCommentGeneratedAt datetime2 NULL,
 CONSTRAINT FK_CSum_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id)
);
CREATE TABLE AuditLogs(
 Id bigint IDENTITY PRIMARY KEY,
 UserId bigint NULL,
 Action nvarchar(128) NOT NULL,
 EntityType nvarchar(128) NOT NULL,
 EntityId nvarchar(128) NOT NULL,
 IpAddress nvarchar(64) NULL,
 MetadataJson nvarchar(max) NULL,
 CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_Audit_User FOREIGN KEY(UserId) REFERENCES Users(Id)
);
GO

CREATE INDEX IX_Students_Class_Active ON Students(StudentClassId,IsActive,StudentCode);
CREATE INDEX IX_ClassSections_Teacher ON ClassSections(TeacherId,IsActive,Semester);
CREATE INDEX IX_Mgmt_Teacher_Active ON ManagementAssignments(TeacherId,IsActive,PositionType);
CREATE INDEX IX_Enrollments_Student ON Enrollments(StudentId,ClassSectionId);
CREATE INDEX IX_FaceEnrollments_Student_Status ON FaceEnrollments(StudentId,Status,StartedAt DESC);
CREATE INDEX IX_FaceImages_Enrollment ON StudentFaceImages(FaceEnrollmentId,IsActive,CapturePose);
CREATE INDEX IX_FaceImages_Student_Hash ON StudentFaceImages(StudentId,Sha256,IsActive);
CREATE INDEX IX_FaceTemplates_Student_Active ON StudentFaceTemplates(StudentId,IsActive,CreatedAt DESC);
CREATE INDEX IX_Videos_Hash ON Videos(Sha256,Status);
CREATE INDEX IX_Sessions_Class_Time ON Sessions(ClassSectionId,ScheduledStart DESC);
CREATE INDEX IX_Sessions_OriginalTeacher_Time ON Sessions(OriginalTeacherId,ScheduledStart DESC);
CREATE INDEX IX_Substitute_Teacher_Status ON SessionSubstitutions(SubstituteTeacherId,Status,AssignedAt DESC);
CREATE INDEX IX_Sessions_Camera_Time ON Sessions(CameraId,ScheduledStart,ScheduledEnd);
CREATE INDEX IX_Stable_Session_State ON StableIdentities(SessionId,State,LastSeenAt DESC);
CREATE INDEX IX_Stable_Session_Student ON StableIdentities(SessionId,StudentId,State);
CREATE INDEX IX_Track_Stable_Time ON TrackSegments(StableIdentityId,StartedAt,EndedAt);
CREATE INDEX IX_Behavior_Session_Student_Time ON BehaviorEvents(SessionId,StudentId,StartedAt);
CREATE INDEX IX_Behavior_Label_Time ON BehaviorEvents(Label,StartedAt);
CREATE INDEX IX_Alerts_Session_Status_Time ON Alerts(SessionId,Status,CreatedAt DESC);
CREATE INDEX IX_AiReceipts_Session_Time ON AiEventReceipts(SessionId,ReceivedAt DESC);
CREATE INDEX IX_Audit_User_Time ON AuditLogs(UserId,CreatedAt DESC);
GO
