SET XACT_ABORT ON;
GO
DECLARE @FacultyId bigint=(SELECT TOP 1 Id FROM Faculties WHERE Code='CNTT');
IF @FacultyId IS NULL THROW 50002,'Run 002_seed.sql first',1;
DECLARE @DeptId bigint=(SELECT TOP 1 Id FROM Departments WHERE Code='HTTT');
IF NOT EXISTS(SELECT 1 FROM StudentClasses WHERE Code='12DHTH01') INSERT StudentClasses(Code,Name,FacultyId,StartYear,IsActive) VALUES('12DHTH01',N'Lớp 12DHTH01',@FacultyId,2025,1);
DECLARE @StudentClassId bigint=(SELECT TOP 1 Id FROM StudentClasses WHERE Code='12DHTH01');
IF NOT EXISTS(SELECT 1 FROM Students WHERE StudentCode='SV001') INSERT Students(StudentCode,FullName,Email,StudentClassId,AnonymousCode,IsActive) VALUES('SV001',N'Nguyễn Văn An','sv001@example.local',@StudentClassId,'ANON-SV001',1);
IF NOT EXISTS(SELECT 1 FROM Students WHERE StudentCode='SV002') INSERT Students(StudentCode,FullName,Email,StudentClassId,AnonymousCode,IsActive) VALUES('SV002',N'Trần Thị Bình','sv002@example.local',@StudentClassId,'ANON-SV002',1);
IF NOT EXISTS(SELECT 1 FROM Teachers WHERE TeacherCode='GV001') INSERT Teachers(TeacherCode,FullName,Email,DepartmentId,IsActive) VALUES('GV001',N'Giảng viên Demo','lecturer@example.local',@DeptId,1);
DECLARE @TeacherId bigint=(SELECT TOP 1 Id FROM Teachers WHERE TeacherCode='GV001');
IF NOT EXISTS(SELECT 1 FROM Courses WHERE Code='HTBAM-DEMO') INSERT Courses(Code,Name,DepartmentId,Credits,IsActive) VALUES('HTBAM-DEMO',N'Phân tích hành vi học tập',@DeptId,3,1);
DECLARE @CourseId bigint=(SELECT TOP 1 Id FROM Courses WHERE Code='HTBAM-DEMO');
IF NOT EXISTS(SELECT 1 FROM ClassSections WHERE Code='HTBAM-DEMO-01') INSERT ClassSections(Code,Name,CourseId,TeacherId,Semester,AcademicYear,IsActive) VALUES('HTBAM-DEMO-01',N'HTBAM Demo 01',@CourseId,@TeacherId,'HK1','2026-2027',1);
DECLARE @ClassSectionId bigint=(SELECT TOP 1 Id FROM ClassSections WHERE Code='HTBAM-DEMO-01');
INSERT Enrollments(ClassSectionId,StudentId) SELECT @ClassSectionId,Id FROM Students s WHERE s.StudentCode IN('SV001','SV002') AND NOT EXISTS(SELECT 1 FROM Enrollments e WHERE e.ClassSectionId=@ClassSectionId AND e.StudentId=s.Id);
IF NOT EXISTS(SELECT 1 FROM Rooms WHERE Code='A101') INSERT Rooms(Code,Name,Location,Capacity,IsActive) VALUES('A101',N'Phòng A101',N'Cơ sở demo',50,1);
DECLARE @RoomId bigint=(SELECT TOP 1 Id FROM Rooms WHERE Code='A101');
IF NOT EXISTS(SELECT 1 FROM Cameras WHERE Code='CAM-A101') INSERT Cameras(Code,Name,RoomId,RtspUrl,Status,IsActive) VALUES('CAM-A101',N'Camera phòng A101',@RoomId,'rtsp://127.0.0.1:8554/classroom','OFFLINE',1);
GO
