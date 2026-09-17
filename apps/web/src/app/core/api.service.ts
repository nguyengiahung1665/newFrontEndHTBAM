import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Alert, AttendancePolicy, Camera, Capabilities, ClassSection, Course, Department, Faculty, FaceEnrollment, Room, Session, Student, StudentClass, StudentStatusFilter, Teacher, Video, AdminUser } from './models';

@Injectable({providedIn:'root'})
export class ApiService {
  constructor(private http:HttpClient) {}
  students(q='',status:StudentStatusFilter='all'){
    let params=new HttpParams().set('q',q).set('status',status).set('includeInactive',String(status!=='active'));
    return this.http.get<Student[]>('/api/students',{params});
  }
  student(id:number){return this.http.get<Student>(`/api/students/${id}`)}
  saveStudent(id:number|null,body:Partial<Student>){return id?this.http.put(`/api/students/${id}`,body):this.http.post('/api/students',body)}
  deactivateStudent(id:number){return this.http.delete(`/api/students/${id}`)}
  reactivateStudent(id:number){return this.http.post(`/api/students/${id}/reactivate`,{})}
  importStudents(file:File){const f=new FormData();f.append('file',file);return this.http.post<{inserted:number;message:string;errors:any[]}>('/api/students/import-csv',f)}

  faculties(){return this.http.get<Faculty[]>('/api/catalogs/faculties')}
  departments(){return this.http.get<Department[]>('/api/catalogs/departments')}
  studentClasses(){return this.http.get<StudentClass[]>('/api/catalogs/student-classes')}
  courses(){return this.http.get<Course[]>('/api/catalogs/courses')}
  teachers(){return this.http.get<Teacher[]>('/api/catalogs/teachers')}
  classSections(){return this.http.get<ClassSection[]>('/api/catalogs/class-sections')}
  rooms(){return this.http.get<Room[]>('/api/catalogs/rooms')}
  cameras(){return this.http.get<Camera[]>('/api/catalogs/cameras')}
  saveCatalog(resource:string,id:number|null,body:any){return id?this.http.put(`/api/catalogs/${resource}/${id}`,body):this.http.post(`/api/catalogs/${resource}`,body)}
  deactivateCatalog(resource:string,id:number){return this.http.delete(`/api/catalogs/${resource}/${id}`)}
  testCamera(id:number){return this.http.post<any>(`/api/catalogs/cameras/${id}/test`,{})}
  roster(id:number){return this.http.get<any[]>(`/api/catalogs/class-sections/${id}/roster`)}
  setRoster(id:number,studentIds:number[]){return this.http.put(`/api/catalogs/class-sections/${id}/roster`,{studentIds})}

  videos(){return this.http.get<Video[]>('/api/videos')}
  uploadVideo(file:File){const f=new FormData();f.append('file',file);return this.http.post<Video>('/api/videos',f)}
  previewVideo(id:number){return this.http.get<{url:string;contentType:string;fileName:string}>(`/api/videos/${id}/preview-url`)}
  deleteVideo(id:number){return this.http.delete(`/api/videos/${id}`)}

  sessions(query=''){return this.http.get<Session[]>(`/api/sessions${query?'?'+query:''}`)}
  session(id:number){return this.http.get<any>(`/api/sessions/${id}`)}
  sessionDashboard(id:number){return this.http.get<any>(`/api/sessions/${id}/dashboard`)}
  createSession(body:any){return this.http.post<any>('/api/sessions',body)}
  updateSession(id:number,body:any){return this.http.put(`/api/sessions/${id}`,body)}
  sessionAction(id:number,action:'start'|'stop'|'cancel'|'retry-finalize'){return this.http.post(`/api/sessions/${id}/${action}`,{})}

  alerts(query=''){return this.http.get<Alert[]>(`/api/alerts${query?'?'+query:''}`)}
  alertAction(id:number,action:'ack'|'close'|'reopen',note=''){return this.http.post(`/api/alerts/${id}/${action}`,{note})}
  alertRules(){return this.http.get<any[]>('/api/alerts/rules')}

  policies(){return this.http.get<AttendancePolicy[]>('/api/attendance-policies')}
  savePolicy(id:number|null,body:any){return id?this.http.put(`/api/attendance-policies/${id}`,body):this.http.post('/api/attendance-policies',body)}

  capabilities(){return this.http.get<Capabilities>('/api/system/capabilities')}
  counts(){return this.http.get<any>('/api/system/counts')}
  search(query:string){return this.http.get<any>(`/api/search?${query}`)}
  audit(query=''){return this.http.get<any[]>(`/api/audit${query?'?'+query:''}`)}
  adminUsers(q=''){return this.http.get<AdminUser[]>(`/api/admin/users?q=${encodeURIComponent(q)}`)}
  saveAdminUser(id:number|null,body:any){return id?this.http.put(`/api/admin/users/${id}`,body):this.http.post('/api/admin/users',body)}
  adminStatus(id:number,status:string){return this.http.put(`/api/admin/users/${id}/status`,{status})}
  historySession(id:number,kind:'behaviors'|'attendance'|'identities'){return this.http.get<any[]>(`/api/history/sessions/${id}/${kind}`)}
  report(kind:'sessions'|'students'|'class-sections',id:number){return this.http.get<any>(`/api/reports/${kind}/${id}`)}
  exportReport(kind:'sessions'|'students'|'class-sections',id:number,format:'pdf'|'excel'){return this.http.get(`/api/reports/${kind}/${id}/${format}`,{responseType:'blob'})}

  faceList(studentId:number){return this.http.get<FaceEnrollment[]>(`/api/students/${studentId}/face-enrollments`)}
  faceDetail(studentId:number,enrollmentId:number){return this.http.get<any>(`/api/students/${studentId}/face-enrollments/${enrollmentId}`)}
  faceStart(studentId:number){return this.http.post<any>(`/api/students/${studentId}/face-enrollments`,{})}
  faceUpload(studentId:number,enrollmentId:number,file:File,pose:string){const f=new FormData();f.append('file',file);f.append('capturePose',pose);f.append('sourceType','UPLOAD');return this.http.post(`/api/students/${studentId}/face-enrollments/${enrollmentId}/images`,f)}
  faceSubmit(studentId:number,enrollmentId:number){return this.http.post(`/api/students/${studentId}/face-enrollments/${enrollmentId}/submit`,{})}
  faceProcess(studentId:number,enrollmentId:number){return this.http.post(`/api/students/${studentId}/face-enrollments/${enrollmentId}/process`,{})}
}
