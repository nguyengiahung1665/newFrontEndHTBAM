/* Upgrade an toàn từ schema v1 đã giao trước đó. Chạy sau khi backup DB. */
USE HTBAM;
GO
IF OBJECT_ID('FaceEnrollments','U') IS NULL
CREATE TABLE FaceEnrollments(Id bigint IDENTITY PRIMARY KEY,StudentId bigint NOT NULL,Status nvarchar(32) NOT NULL DEFAULT 'IN_PROGRESS',RequiredPoses nvarchar(128) NOT NULL DEFAULT 'FRONT,LEFT,RIGHT',MinAcceptedImages int NOT NULL DEFAULT 6,AcceptedImageCount int NOT NULL DEFAULT 0,FailureReason nvarchar(1000) NULL,StartedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),SubmittedAt datetime2 NULL,CompletedAt datetime2 NULL,CONSTRAINT FK_FaceEnrollments_Student FOREIGN KEY(StudentId) REFERENCES Students(Id));
IF OBJECT_ID('StudentFaceImages','U') IS NULL
CREATE TABLE StudentFaceImages(Id bigint IDENTITY PRIMARY KEY,FaceEnrollmentId bigint NOT NULL,StudentId bigint NOT NULL,ObjectKey nvarchar(512) NOT NULL UNIQUE,OriginalFileName nvarchar(256) NOT NULL,ContentType nvarchar(128) NOT NULL,SizeBytes bigint NOT NULL,Sha256 char(64) NOT NULL,CapturePose nvarchar(32) NOT NULL,SourceType nvarchar(32) NOT NULL,QualityScore decimal(8,5) NULL,QualityStatus nvarchar(32) NOT NULL DEFAULT 'PENDING',QualityReason nvarchar(1000) NULL,IsActive bit NOT NULL DEFAULT 1,CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),ProcessedAt datetime2 NULL,CONSTRAINT FK_FaceImages_Enrollment FOREIGN KEY(FaceEnrollmentId) REFERENCES FaceEnrollments(Id),CONSTRAINT FK_FaceImages_Student FOREIGN KEY(StudentId) REFERENCES Students(Id));
IF OBJECT_ID('AiEventReceipts','U') IS NULL
CREATE TABLE AiEventReceipts(Id bigint IDENTITY PRIMARY KEY,ExternalEventId nvarchar(128) NOT NULL UNIQUE,SessionId bigint NOT NULL,EventType nvarchar(64) NOT NULL,EventTimestamp datetime2 NOT NULL,ReceivedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CONSTRAINT FK_AiReceipt_Session FOREIGN KEY(SessionId) REFERENCES Sessions(Id));
GO
IF COL_LENGTH('StudentFaceTemplates','FaceEnrollmentId') IS NULL ALTER TABLE StudentFaceTemplates ADD FaceEnrollmentId bigint NULL;
IF COL_LENGTH('StudentFaceTemplates','ModelName') IS NULL ALTER TABLE StudentFaceTemplates ADD ModelName nvarchar(128) NOT NULL CONSTRAINT DF_FaceTemplate_ModelName DEFAULT 'ArcFace';
IF COL_LENGTH('StudentFaceTemplates','EmbeddingDimension') IS NULL ALTER TABLE StudentFaceTemplates ADD EmbeddingDimension int NOT NULL CONSTRAINT DF_FaceTemplate_Dim DEFAULT 512;
IF COL_LENGTH('StudentFaceTemplates','SourceImageCount') IS NULL ALTER TABLE StudentFaceTemplates ADD SourceImageCount int NOT NULL CONSTRAINT DF_FaceTemplate_SourceCount DEFAULT 0;
IF COL_LENGTH('StudentFaceTemplates','IsActive') IS NULL ALTER TABLE StudentFaceTemplates ADD IsActive bit NOT NULL CONSTRAINT DF_FaceTemplate_Active DEFAULT 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_FaceTemplates_Enrollment') AND COL_LENGTH('StudentFaceTemplates','FaceEnrollmentId') IS NOT NULL
    ALTER TABLE StudentFaceTemplates ADD CONSTRAINT FK_FaceTemplates_Enrollment FOREIGN KEY(FaceEnrollmentId) REFERENCES FaceEnrollments(Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FaceTemplates_Student_Active' AND object_id=OBJECT_ID('StudentFaceTemplates'))
    CREATE INDEX IX_FaceTemplates_Student_Active ON StudentFaceTemplates(StudentId,IsActive,CreatedAt DESC);
IF COL_LENGTH('Videos','ContentType') IS NULL ALTER TABLE Videos ADD ContentType nvarchar(128) NOT NULL CONSTRAINT DF_Videos_ContentType DEFAULT 'video/mp4';
IF COL_LENGTH('Videos','Sha256') IS NULL ALTER TABLE Videos ADD Sha256 char(64) NOT NULL CONSTRAINT DF_Videos_Sha DEFAULT '';
IF COL_LENGTH('Videos','Status') IS NULL ALTER TABLE Videos ADD Status nvarchar(32) NOT NULL CONSTRAINT DF_Videos_Status DEFAULT 'READY';
IF COL_LENGTH('Sessions','RowVersion') IS NULL ALTER TABLE Sessions ADD RowVersion rowversion;
IF COL_LENGTH('AnalysisJobs','ErrorMessage') IS NULL ALTER TABLE AnalysisJobs ADD ErrorMessage nvarchar(1000) NULL;
IF COL_LENGTH('StableIdentities','CurrentTrackId') IS NULL ALTER TABLE StableIdentities ADD CurrentTrackId nvarchar(128) NULL;
IF COL_LENGTH('StableIdentities','LastZone') IS NULL ALTER TABLE StableIdentities ADD LastZone nvarchar(128) NULL;
IF COL_LENGTH('StableIdentities','SeatHint') IS NULL ALTER TABLE StableIdentities ADD SeatHint nvarchar(128) NULL;
IF COL_LENGTH('StableIdentities','ModelVersion') IS NULL ALTER TABLE StableIdentities ADD ModelVersion nvarchar(128) NOT NULL CONSTRAINT DF_Stable_ModelVersion DEFAULT '';
IF COL_LENGTH('TrackSegments','ObservationCount') IS NULL ALTER TABLE TrackSegments ADD ObservationCount int NOT NULL CONSTRAINT DF_Track_ObsCount DEFAULT 1;
IF COL_LENGTH('IdentityLinks','Decision') IS NULL ALTER TABLE IdentityLinks ADD Decision nvarchar(32) NOT NULL CONSTRAINT DF_IL_Decision DEFAULT 'EVIDENCE';
IF COL_LENGTH('AlertRules','MinObservationQuality') IS NULL ALTER TABLE AlertRules ADD MinObservationQuality decimal(8,5) NOT NULL CONSTRAINT DF_AR_MinQuality DEFAULT 0.5;
IF COL_LENGTH('Alerts','EvidenceRef') IS NULL ALTER TABLE Alerts ADD EvidenceRef nvarchar(512) NULL;
GO
UPDATE AlertRules SET BehaviorLabel='PHONE_USE' WHERE Code='PHONE' AND BehaviorLabel='PHONE';
GO
