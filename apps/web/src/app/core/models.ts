export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  userId: number;
  fullName: string;
  roles: string[];
  tokenVersion: number;
}

export interface UserInfo {
  id: number;
  userName: string;
  email: string;
  fullName: string;
  status: string;
  roles: string[];
}
export interface Student { id:number; studentCode:string; fullName:string; email:string; studentClassId?:number|null; studentClass?:string|null; anonymousCode:string; isActive:boolean; createdAt?:string; }
export interface CatalogBase { id:number; code:string; name:string; note?:string|null; isActive:boolean; }
export interface Faculty extends CatalogBase {}
export interface Department extends CatalogBase { facultyId:number; faculty?:string; }
export interface StudentClass extends CatalogBase { facultyId:number; faculty?:string; startYear?:number|null; }
export interface Course extends CatalogBase { departmentId?:number|null; credits:number; }
export interface Teacher { id:number; teacherCode:string; fullName:string; email:string; userId?:number|null; departmentId?:number|null; note?:string|null; isActive:boolean; }
export interface ClassSection extends CatalogBase { courseId:number; course?:string; teacherId:number; teacher?:string; semester:string; academicYear:string; }
export interface Room extends CatalogBase { location?:string; capacity:number; }
export interface Camera extends CatalogBase { roomId:number; room?:string; status:string; lastHealthCheckAt?:string|null; lastHealthMessage?:string|null; rtspUrl?:string; }
export interface Video { id:number; fileName:string; contentType:string; sizeBytes:number; status:string; uploadedAt:string; uploadedByUserId?:number|null; }
export interface Session { id:number; classSectionId:number; scheduledStart:string; scheduledEnd?:string|null; startedAt?:string|null; endedAt?:string|null; status:string; cameraId?:number|null; videoId?:number|null; attendancePolicyId:number; }
export interface AttendancePolicy { id:number; code:string; name:string; presentThreshold:number; partialThreshold:number; presentScore:number; partialScore:number; absentScore:number; version:string; isActive:boolean; }
export interface Alert { id:number; sessionId:number; studentId?:number|null; type:string; status:string; confidence:number; observationQuality:number; startedAt:string; endedAt?:string|null; createdAt:string; lecturerNote?:string|null; }
export interface AdminUser { id:number; userName:string; email:string; fullName:string; status:string; tokenVersion:number; lastLoginAt?:string|null; roles:string[]; }
export interface FaceEnrollment { id:number; studentId:number; status:string; requiredPoses:string; minAcceptedImages:number; acceptedImageCount:number; uploadedImageCount:number; startedAt:string; }
export interface Capabilities { web:boolean; backend:boolean; database:boolean; objectStorage:boolean; aiHealth:boolean; sessionInference:boolean; faceEnrollment:boolean; loadedModels:string[]; llmProvider:string; modelIntegrationPending:boolean; }
export interface ApiErrorItem { line?:number; field?:string|null; message:string; }
export type StudentStatusFilter = 'all'|'active'|'inactive';
