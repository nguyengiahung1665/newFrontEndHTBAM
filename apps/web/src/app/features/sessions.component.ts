import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AttendancePolicy, Camera, ClassSection, Room, Session, Video } from '../core/models';
import { ModalComponent, PageTitleComponent, errorText, fmtDate } from '../shared/ui';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink, PageTitleComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-title title="Buổi học" subtitle="Quản lý và theo dõi các phiên phân tích trong học kỳ.">
      <button type="button" (click)="openCreate()">+ Tạo buổi học</button>
    </app-page-title>

    @if (error()) { <div class="error-box">{{ error() }}</div> }
    @if (message()) { <div class="success-box">{{ message() }}</div> }
    @if (loading()) { <div class="notice">Đang tải danh sách buổi học…</div> }

    <div class="filter-bar">
      <label class="grow">Tìm buổi học<input [(ngModel)]="search" placeholder="ID hoặc lớp học phần…" /></label>
      <label>Trạng thái
        <select [(ngModel)]="statusFilter">
          <option value="">Tất cả</option>
          <option value="RUNNING">Đang diễn ra</option>
          <option value="COMPLETED">Đã kết thúc</option>
          <option value="READY">Sẵn sàng</option>
          <option value="DRAFT">Bản nháp</option>
          <option value="CANCELLED">Đã hủy</option>
        </select>
      </label>
      <button type="button" class="secondary" (click)="search='';statusFilter=''">Xóa lọc</button>
    </div>

    <section class="card table-card">
      <div class="table-wrap">
        <table>
          <thead><tr><th>Buổi học</th><th>Lớp học phần</th><th>Nguồn</th><th>Thời gian</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>
            @for (session of filteredItems(); track session.id) {
              <tr>
                <td class="cell-title">#{{ session.id }}</td>
                <td>{{ sectionLabel(session.classSectionId) }}</td>
                <td><span class="badge" [class.purple]="session.videoId">{{ session.cameraId ? 'Camera' : session.videoId ? 'Video' : 'Chưa chọn' }}</span></td>
                <td>{{ date(session.scheduledStart) }}<small>@if (session.scheduledEnd) { đến {{ date(session.scheduledEnd) }} }</small></td>
                <td><span class="badge" [class.gray]="session.status === 'COMPLETED' || session.status === 'CANCELLED'" [class.blue]="session.status === 'READY' || session.status === 'DRAFT'" [class.danger]="session.status === 'FAILED'">{{ statusLabel(session.status) }}</span></td>
                <td><a class="button-link secondary small" [routerLink]="['/sessions', session.id]">Xem chi tiết</a></td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="empty">Không có buổi học phù hợp.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>

    @if (createOpen()) {
      <app-modal title="Tạo buổi học" [busy]="saving()" size="lg" (close)="closeCreate()">
        @if (formError()) { <div class="error-box">{{ formError() }}</div> }
        <form class="form-grid" (ngSubmit)="createSession()">
          <label class="span2">Lớp học phần <span class="required">*</span>
            <select [(ngModel)]="form.classSectionId" name="classSectionId" required>
              <option [ngValue]="null">-- Chọn lớp học phần --</option>
              @for (section of sections(); track section.id) { <option [ngValue]="section.id">{{ section.code }} - {{ section.name }}</option> }
            </select>
          </label>
          <label>Ngày <span class="required">*</span><input type="date" [(ngModel)]="form.date" name="date" required /></label>
          <div class="form-grid">
            <label>Giờ bắt đầu <span class="required">*</span><input type="time" [(ngModel)]="form.startTime" name="startTime" required /></label>
            <label>Giờ kết thúc<input type="time" [(ngModel)]="form.endTime" name="endTime" /></label>
          </div>
          <label>Phòng học
            <select [(ngModel)]="form.roomId" name="roomId"><option [ngValue]="null">-- Chọn phòng --</option>@for (room of rooms(); track room.id) { <option [ngValue]="room.id">{{ room.code }} - {{ room.name }}</option> }</select>
          </label>
          <label>Chính sách chuyên cần <span class="required">*</span>
            <select [(ngModel)]="form.attendancePolicyId" name="attendancePolicyId" required><option [ngValue]="null">-- Chọn chính sách --</option>@for (policy of policies(); track policy.id) { <option [ngValue]="policy.id">{{ policy.code }} - {{ policy.name }}</option> }</select>
          </label>
          <div class="span2">
            <label>Nguồn phân tích <span class="required">*</span></label>
            <div class="inline-actions" style="margin-top:8px">
              <button type="button" [class.secondary]="form.source !== 'camera'" (click)="form.source='camera';form.videoId=null">Camera trực tiếp</button>
              <button type="button" [class.secondary]="form.source !== 'video'" (click)="form.source='video';form.cameraId=null">Video tải lên</button>
            </div>
          </div>
          @if (form.source === 'camera') {
            <label class="span2">Camera <span class="required">*</span><select [(ngModel)]="form.cameraId" name="cameraId"><option [ngValue]="null">-- Chọn camera --</option>@for (camera of cameras(); track camera.id) { <option [ngValue]="camera.id">{{ camera.code }} - {{ camera.name }}</option> }</select></label>
          } @else {
            <label class="span2">Video <span class="required">*</span><select [(ngModel)]="form.videoId" name="videoId"><option [ngValue]="null">-- Chọn video --</option>@for (video of readyVideos(); track video.id) { <option [ngValue]="video.id">{{ video.fileName }}</option> }</select></label>
          }
          <label class="span2">Hồ sơ cảnh báo<input [(ngModel)]="form.alertProfile" name="alertProfile" placeholder="DEFAULT" /></label>
          <div class="modal-footer span2" style="margin:0 -20px -20px">
            <button type="button" class="secondary" [disabled]="saving()" (click)="closeCreate()">Hủy</button>
            <button type="submit" [disabled]="saving()">{{ saving() ? 'Đang tạo…' : 'Tạo buổi học' }}</button>
          </div>
        </form>
      </app-modal>
    }
  `,
})
export class SessionsComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly items = signal<Session[]>([]);
  readonly sections = signal<ClassSection[]>([]);
  readonly rooms = signal<Room[]>([]);
  readonly cameras = signal<Camera[]>([]);
  readonly videos = signal<Video[]>([]);
  readonly policies = signal<AttendancePolicy[]>([]);
  readonly error = signal('');
  readonly message = signal('');
  readonly formError = signal('');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly createOpen = signal(false);
  readonly date = fmtDate;
  search = '';
  statusFilter = '';
  form = this.emptyForm();

  ngOnInit(): void {
    this.loading.set(true);
    forkJoin({
      sessions: this.api.sessions(), sections: this.api.classSections(), rooms: this.api.rooms(),
      cameras: this.api.cameras(), videos: this.api.videos(), policies: this.api.policies(),
    }).subscribe({
      next: (result) => {
        this.items.set(result.sessions); this.sections.set(result.sections); this.rooms.set(result.rooms);
        this.cameras.set(result.cameras); this.videos.set(result.videos); this.policies.set(result.policies);
        this.loading.set(false);
      },
      error: (error) => { this.error.set(errorText(error)); this.loading.set(false); },
    });
  }

  filteredItems(): Session[] {
    const query = this.search.trim().toLowerCase();
    return this.items().filter((item) =>
      (!query || String(item.id).includes(query) || this.sectionLabel(item.classSectionId).toLowerCase().includes(query)) &&
      (!this.statusFilter || item.status === this.statusFilter),
    );
  }

  readyVideos(): Video[] { return this.videos().filter((item) => item.status === 'READY'); }
  sectionLabel(id: number): string { const item = this.sections().find((section) => section.id === id); return item ? `${item.code} - ${item.name}` : `#${id}`; }
  statusLabel(status: string): string { return ({ RUNNING: 'Đang diễn ra', COMPLETED: 'Đã kết thúc', READY: 'Sẵn sàng', DRAFT: 'Bản nháp', CANCELLED: 'Đã hủy', FAILED: 'Lỗi' } as Record<string, string>)[status] ?? status; }

  openCreate(): void { this.form = this.emptyForm(); this.formError.set(''); this.createOpen.set(true); }
  closeCreate(): void { if (!this.saving()) this.createOpen.set(false); }

  createSession(): void {
    if (!this.form.classSectionId || !this.form.attendancePolicyId || !this.form.date || !this.form.startTime || (this.form.source === 'camera' ? !this.form.cameraId : !this.form.videoId)) {
      this.formError.set('Vui lòng nhập đầy đủ các trường bắt buộc.');
      return;
    }
    const start = new Date(`${this.form.date}T${this.form.startTime}`);
    const end = this.form.endTime ? new Date(`${this.form.date}T${this.form.endTime}`) : null;
    if (end && end <= start) { this.formError.set('Giờ kết thúc phải sau giờ bắt đầu.'); return; }

    this.saving.set(true);
    this.formError.set('');
    this.api.createSession({
      classSectionId: this.form.classSectionId,
      roomId: this.form.roomId,
      cameraId: this.form.source === 'camera' ? this.form.cameraId : null,
      videoId: this.form.source === 'video' ? this.form.videoId : null,
      attendancePolicyId: this.form.attendancePolicyId,
      scheduledStart: start.toISOString(),
      scheduledEnd: end?.toISOString() ?? null,
      studentIds: [],
      alertProfile: this.form.alertProfile.trim() || 'DEFAULT',
    }).subscribe({
      next: () => { this.saving.set(false); this.createOpen.set(false); this.message.set('Tạo buổi học thành công.'); this.reloadSessions(); },
      error: (error) => { this.formError.set(errorText(error)); this.saving.set(false); },
    });
  }

  private reloadSessions(): void {
    this.api.sessions().subscribe({ next: (items) => this.items.set(items), error: (error) => this.error.set(errorText(error)) });
  }

  private emptyForm() {
    return {
      classSectionId: null as number | null, roomId: null as number | null,
      attendancePolicyId: null as number | null, date: '', startTime: '', endTime: '',
      source: 'camera' as 'camera' | 'video', cameraId: null as number | null, videoId: null as number | null,
      alertProfile: 'DEFAULT',
    };
  }
}
