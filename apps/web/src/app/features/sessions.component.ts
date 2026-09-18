import { ChangeDetectionStrategy, Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, Observable, of } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { AttendancePolicy, Camera, ClassSection, Room, Session, Teacher, Video } from '../core/models';
import { PageTitleComponent, errorText, fmtDate } from '../shared/ui';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink, PageTitleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-title title="Buoi hoc" subtitle="Quan ly phien hoc theo scope giang day va quan ly.">
      <button type="button" (click)="startAdd()">+ Them</button>
    </app-page-title>

    @if (error()) { <div class="error-box">{{ error() }}</div> }
    @for (loadError of loadErrorList(); track loadError.key) { <div class="error-box">{{ loadError.label }}: {{ loadError.message }}</div> }
    @if (message()) { <div class="success-box">{{ message() }}</div> }
    @if (loading()) { <div class="notice">Dang tai danh sach buoi hoc...</div> }

    <div class="filter-bar">
      <label class="grow">Tim buoi hoc<input [(ngModel)]="search" placeholder="ID hoac lop hoc phan" /></label>
      <label>Trang thai
        <select [(ngModel)]="statusFilter">
          <option value="">Tat ca</option>
          <option value="RUNNING">Dang dien ra</option>
          <option value="COMPLETED">Da ket thuc</option>
          <option value="READY">San sang</option>
          <option value="DRAFT">Ban nhap</option>
          <option value="CANCELLED">Da huy</option>
        </select>
      </label>
      <button type="button" class="secondary" (click)="search='';statusFilter=''">Xoa loc</button>
    </div>

    <section class="card table-card">
      <div class="table-wrap">
        <table>
          <thead><tr><th>Buoi hoc</th><th>Lop hoc phan</th><th>Giang vien</th><th>Nguon</th><th>Thoi gian</th><th>Trang thai</th><th>Thao tac</th></tr></thead>
          <tbody>
            @if (adding()) {
              <tr>
                <td class="cell-title">Moi</td>
                <td><select [(ngModel)]="form.classSectionId"><option [ngValue]="null">-- Lop --</option>@for (section of sections(); track section.id) { <option [ngValue]="section.id">{{ section.code }} - {{ section.name }}</option> }</select></td>
                <td>GV goc theo lop</td>
                <td>
                  <select [(ngModel)]="form.source"><option value="camera">Camera</option><option value="video">Video</option></select>
                  @if (form.source === 'camera') { <select [(ngModel)]="form.cameraId"><option [ngValue]="null">-- Camera --</option>@for (camera of cameras(); track camera.id) { <option [ngValue]="camera.id">{{ camera.code }}</option> }</select> }
                  @else { <select [(ngModel)]="form.videoId"><option [ngValue]="null">-- Video --</option>@for (video of readyVideos(); track video.id) { <option [ngValue]="video.id">{{ video.fileName }}</option> }</select> }
                </td>
                <td><input type="date" [(ngModel)]="form.date" /><input type="time" [(ngModel)]="form.startTime" /><input type="time" [(ngModel)]="form.endTime" /></td>
                <td><select [(ngModel)]="form.attendancePolicyId"><option [ngValue]="null">-- Chinh sach --</option>@for (policy of policies(); track policy.id) { <option [ngValue]="policy.id">{{ policy.code }}</option> }</select></td>
                <td><div class="inline-actions"><button type="button" class="small" [disabled]="saving()" (click)="createSession()">Luu</button><button type="button" class="secondary small" [disabled]="saving()" (click)="cancelAdd()">Huy</button></div></td>
              </tr>
              @if (formError()) { <tr><td colspan="7" class="error-box">{{ formError() }}</td></tr> }
            }
            @for (session of filteredItems(); track session.id) {
              <tr>
                <td class="cell-title">#{{ session.id }}</td>
                <td>{{ sectionLabel(session.classSectionId) }}</td>
                <td>
                  <div>Giảng viên phụ trách: {{ session.originalTeacherName || '-' }}</div>
                  <small>Người dạy thực tế: {{ session.activeSubstitute?.substituteTeacherName || session.originalTeacherName || '-' }}</small>
                  @if (session.isSubstituteTeaching) { <small><span class="badge blue">Buổi dạy thay</span></small> }
                  @else if (session.activeSubstitute) { <small><span class="badge purple">Đã giao cho {{ session.activeSubstitute.substituteTeacherName }}</span></small> }
                </td>
                <td><span class="badge" [class.purple]="session.videoId">{{ session.cameraId ? 'Camera' : session.videoId ? 'Video' : 'Chua chon' }}</span></td>
                <td>{{ date(session.scheduledStart) }}<small>@if (session.scheduledEnd) { den {{ date(session.scheduledEnd) }} }</small></td>
                <td><span class="badge" [class.gray]="session.status === 'COMPLETED' || session.status === 'CANCELLED'" [class.blue]="session.status === 'READY' || session.status === 'DRAFT'">{{ statusLabel(session.status) }}</span></td>
                <td class="menu-cell">
                  <button type="button" class="icon-button" (click)="toggleMenu(session.id, $event)" aria-label="Thao tác">⋮</button>
                  @if (openMenuId() === session.id) {
                    <div class="action-menu">
                      <a [routerLink]="['/sessions', session.id]">Xem chi tiet</a>
                      @if (session.canManage && (session.status === 'READY' || session.status === 'DRAFT')) { <button type="button" (click)="startEdit(session)">Sửa</button> }
                      @if (auth.canAssignSubstitute() && !session.activeSubstitute) { <button type="button" (click)="assignSubstitute(session)">Phan cong day thay</button> }
                      @if (auth.canAssignSubstitute() && session.activeSubstitute) { <button type="button" (click)="cancelSubstitute(session)">Huy phan cong day thay</button> }
                      @if (session.canManage && (session.status === 'READY' || session.status === 'DRAFT')) { <button type="button" (click)="cancelSession(session)">Huy buoi hoc</button> }
                    </div>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="empty">Khong co buoi hoc phu hop.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>
    @if (substitutionSession()) {
      <section class="card action-panel">
        <div class="card-header"><div><h3>Phân công dạy thay</h3><p>Buổi học #{{ substitutionSession()!.id }}</p></div></div>
        @if (substitutionError()) { <div class="error-box">{{ substitutionError() }}</div> }
        <div class="form-grid">
          <label>Giảng viên dạy thay <select [(ngModel)]="substitutionForm.teacherId"><option [ngValue]="null">-- Chọn giảng viên --</option>@for (teacher of teachers(); track teacher.id) { <option [ngValue]="teacher.id">{{ teacher.teacherCode }} - {{ teacher.fullName }}</option> }</select></label>
          <label>Lý do <input [(ngModel)]="substitutionForm.reason" /></label>
          <label class="span2">Ghi chú<textarea [(ngModel)]="substitutionForm.note" rows="3"></textarea></label>
        </div>
        <div class="inline-actions"><button type="button" [disabled]="substitutionSaving()" (click)="saveSubstitution()">{{ substitutionSaving() ? 'Đang lưu...' : 'Lưu phân công' }}</button><button type="button" class="secondary" [disabled]="substitutionSaving()" (click)="closeSubstitution()">Hủy</button></div>
      </section>
    }
  `,
})
export class SessionsComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly auth = inject(AuthService);
  readonly items = signal<Session[]>([]);
  readonly sections = signal<ClassSection[]>([]);
  readonly rooms = signal<Room[]>([]);
  readonly cameras = signal<Camera[]>([]);
  readonly videos = signal<Video[]>([]);
  readonly policies = signal<AttendancePolicy[]>([]);
  readonly teachers = signal<Teacher[]>([]);
  readonly error = signal('');
  readonly loadErrors = signal<Record<string, string>>({});
  readonly message = signal('');
  readonly formError = signal('');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly adding = signal(false);
  readonly openMenuId = signal<number | null>(null);
  readonly editingSessionId = signal<number | null>(null);
  readonly substitutionSession = signal<Session | null>(null);
  readonly substitutionSaving = signal(false);
  readonly substitutionError = signal('');
  readonly date = fmtDate;
  search = '';
  statusFilter = '';
  form = this.emptyForm();
  substitutionForm = { teacherId: null as number | null, reason: '', note: '' };

  @HostListener('document:click') closeMenus(): void { this.openMenuId.set(null); }

  ngOnInit(): void {
    this.loading.set(true);
    this.loadErrors.set({});
    forkJoin({
      sessions: this.withFallback('sessions', [], this.api.sessions()), sections: this.withFallback('sections', [], this.api.classSections()), rooms: this.withFallback('rooms', [], this.api.rooms()),
      cameras: this.withFallback('cameras', [], this.api.cameras()), videos: this.withFallback('videos', [], this.api.videos()), policies: this.withFallback('policies', [], this.api.policies()), teachers: this.withFallback('teachers', [], this.api.teachers()),
    }).subscribe({
      next: (result) => {
        this.items.set(result.sessions); this.sections.set(result.sections); this.rooms.set(result.rooms);
        this.cameras.set(result.cameras); this.videos.set(result.videos); this.policies.set(result.policies); this.teachers.set(result.teachers);
        this.loading.set(false);
      },
      error: (error) => { this.error.set(errorText(error)); this.loading.set(false); },
    });
  }

  filteredItems(): Session[] {
    const query = this.search.trim().toLowerCase();
    return this.items().filter((item) => (!query || String(item.id).includes(query) || this.sectionLabel(item.classSectionId).toLowerCase().includes(query)) && (!this.statusFilter || item.status === this.statusFilter));
  }

  readyVideos(): Video[] { return this.videos().filter((item) => item.status === 'READY'); }
  loadErrorList(): Array<{key: string; label: string; message: string}> { const labels: Record<string, string> = { sessions: 'Buổi học', sections: 'Lớp học phần', rooms: 'Phòng học', cameras: 'Camera', videos: 'Video', policies: 'Chính sách điểm danh', teachers: 'Giảng viên' }; return Object.entries(this.loadErrors()).map(([key, message]) => ({ key, label: labels[key] ?? key, message })); }
  sectionLabel(id: number): string { const item = this.sections().find((section) => section.id === id); return item ? `${item.code} - ${item.name}` : `#${id}`; }
  statusLabel(status: string): string { return ({ RUNNING: 'Dang dien ra', COMPLETED: 'Da ket thuc', READY: 'San sang', DRAFT: 'Ban nhap', CANCELLED: 'Da huy', FAILED: 'Loi' } as Record<string, string>)[status] ?? status; }

  startAdd(): void { this.editingSessionId.set(null); this.form = this.emptyForm(); this.formError.set(''); this.adding.set(true); }
  startEdit(session: Session): void { const start=new Date(session.scheduledStart);const end=session.scheduledEnd?new Date(session.scheduledEnd):null;this.editingSessionId.set(session.id);this.form={...this.emptyForm(),classSectionId:session.classSectionId,cameraId:session.cameraId??null,videoId:session.videoId??null,attendancePolicyId:session.attendancePolicyId,date:start.toISOString().slice(0,10),startTime:start.toTimeString().slice(0,5),endTime:end?.toTimeString().slice(0,5)??'',source:session.videoId?'video':'camera'};this.adding.set(true);this.openMenuId.set(null); }
  cancelAdd(): void { if (!this.saving()) { this.adding.set(false); this.editingSessionId.set(null); } }
  toggleMenu(id: number, event: MouseEvent): void { event.stopPropagation(); this.openMenuId.set(this.openMenuId() === id ? null : id); }

  createSession(): void {
    if (!this.form.classSectionId || !this.form.attendancePolicyId || !this.form.date || !this.form.startTime || (this.form.source === 'camera' ? !this.form.cameraId : !this.form.videoId)) {
      this.formError.set('Vui long nhap du cac truong bat buoc.');
      return;
    }
    const start = new Date(`${this.form.date}T${this.form.startTime}`);
    const end = this.form.endTime ? new Date(`${this.form.date}T${this.form.endTime}`) : null;
    if (end && end <= start) { this.formError.set('Gio ket thuc phai sau gio bat dau.'); return; }
    this.saving.set(true); this.formError.set('');
    const request = {
      classSectionId: this.form.classSectionId,
      roomId: this.form.roomId,
      cameraId: this.form.source === 'camera' ? this.form.cameraId : null,
      videoId: this.form.source === 'video' ? this.form.videoId : null,
      attendancePolicyId: this.form.attendancePolicyId,
      scheduledStart: start.toISOString(),
      scheduledEnd: end?.toISOString() ?? null,
      studentIds: [],
      alertProfile: 'DEFAULT',
    };
    const operation = this.editingSessionId() ? this.api.updateSession(this.editingSessionId()!, request) : this.api.createSession(request);
    operation.subscribe({
      next: () => { this.saving.set(false); this.adding.set(false); this.editingSessionId.set(null); this.message.set('Đã lưu buổi học.'); this.reloadSessions(); },
      error: (error) => { this.formError.set(errorText(error)); this.saving.set(false); },
    });
  }

  assignSubstitute(session: Session): void {
    this.substitutionSession.set(session); this.substitutionForm={teacherId:null,reason:'',note:''};this.substitutionError.set('');this.openMenuId.set(null);
  }
  closeSubstitution(): void { if(!this.substitutionSaving())this.substitutionSession.set(null); }
  saveSubstitution(): void { const session=this.substitutionSession();if(!session||!this.substitutionForm.teacherId||!this.substitutionForm.reason.trim()){this.substitutionError.set('Vui lòng chọn giảng viên và nhập lý do.');return}this.substitutionSaving.set(true);this.api.assignSubstitution(session.id,{substituteTeacherId:this.substitutionForm.teacherId,reason:this.substitutionForm.reason.trim(),note:this.substitutionForm.note.trim()||undefined}).subscribe({next:()=>{this.substitutionSaving.set(false);this.substitutionSession.set(null);this.message.set('Đã phân công dạy thay.');this.reloadSessions()},error:error=>{this.substitutionSaving.set(false);this.substitutionError.set(errorText(error))}})}

  cancelSubstitute(session: Session): void {
    this.api.cancelSubstitution(session.id).subscribe({ next: () => { this.message.set('Da huy phan cong day thay.'); this.reloadSessions(); }, error: (error) => this.error.set(errorText(error)) });
  }

  cancelSession(session: Session): void {
    this.api.sessionAction(session.id, 'cancel').subscribe({ next: () => { this.message.set('Da huy buoi hoc.'); this.reloadSessions(); }, error: (error) => this.error.set(errorText(error)) });
  }

  private reloadSessions(): void {
    this.api.sessions().subscribe({ next: (items) => this.items.set(items), error: (error) => this.error.set(errorText(error)) });
  }

  private emptyForm() {
    return { classSectionId: null as number | null, roomId: null as number | null, attendancePolicyId: null as number | null, date: '', startTime: '', endTime: '', source: 'camera' as 'camera' | 'video', cameraId: null as number | null, videoId: null as number | null };
  }

  private withFallback<T>(key: string, fallback: T, source: Observable<T>): Observable<T> {
    return source.pipe(catchError((error) => { this.loadErrors.update((errors) => ({ ...errors, [key]: errorText(error) })); return of(fallback); }));
  }
}
