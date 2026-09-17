SET XACT_ABORT ON;
GO
IF NOT EXISTS(SELECT 1 FROM Roles WHERE Name='ADMIN') INSERT Roles(Name) VALUES('ADMIN');
IF NOT EXISTS(SELECT 1 FROM Roles WHERE Name='LECTURER') INSERT Roles(Name) VALUES('LECTURER');
IF NOT EXISTS(SELECT 1 FROM Roles WHERE Name='TECH_AI') INSERT Roles(Name) VALUES('TECH_AI');

IF NOT EXISTS(SELECT 1 FROM Faculties WHERE Code='CNTT') INSERT Faculties(Code,Name,IsActive) VALUES('CNTT',N'Khoa Công nghệ thông tin',1);
DECLARE @FacultyId bigint=(SELECT TOP 1 Id FROM Faculties WHERE Code='CNTT');
IF NOT EXISTS(SELECT 1 FROM Departments WHERE Code='HTTT') INSERT Departments(Code,Name,FacultyId,IsActive) VALUES('HTTT',N'Bộ môn Hệ thống thông tin',@FacultyId,1);
IF NOT EXISTS(SELECT 1 FROM AttendancePolicies WHERE Code='DEFAULT')
 INSERT AttendancePolicies(Code,Name,PresentThreshold,PartialThreshold,PresentScore,PartialScore,AbsentScore,Version,IsActive)
 VALUES('DEFAULT',N'Chính sách chuyên cần mặc định',0.80,0.50,10,5,0,'v1',1);

IF NOT EXISTS(SELECT 1 FROM AlertRules WHERE Code='DISTRACTION_LONG') INSERT AlertRules(Code,Name,BehaviorLabel,MinDurationSeconds,MinConfidence,MinObservationQuality,Enabled,Version) VALUES('DISTRACTION_LONG',N'Mất tập trung kéo dài','DISTRACTED',15,0.70,0.55,1,'v1');
IF NOT EXISTS(SELECT 1 FROM AlertRules WHERE Code='SLEEP') INSERT AlertRules(Code,Name,BehaviorLabel,MinDurationSeconds,MinConfidence,MinObservationQuality,Enabled,Version) VALUES('SLEEP',N'Ngủ gật','SLEEPY',10,0.75,0.55,1,'v1');
IF NOT EXISTS(SELECT 1 FROM AlertRules WHERE Code='PHONE') INSERT AlertRules(Code,Name,BehaviorLabel,MinDurationSeconds,MinConfidence,MinObservationQuality,Enabled,Version) VALUES('PHONE',N'Sử dụng điện thoại','PHONE_USE',5,0.70,0.55,1,'v1');
IF NOT EXISTS(SELECT 1 FROM AlertRules WHERE Code='OUT_OF_VIEW') INSERT AlertRules(Code,Name,BehaviorLabel,MinDurationSeconds,MinConfidence,MinObservationQuality,Enabled,Version) VALUES('OUT_OF_VIEW',N'Rời vùng quan sát','OUT_OF_VIEW',20,0.80,0.00,1,'v1');
GO
