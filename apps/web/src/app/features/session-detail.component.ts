import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { catchError, forkJoin, of } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { BackButtonComponent, ModalComponent, PageTitleComponent, behaviorText, errorText, fmtDate, statusText } from '../shared/ui';

@Component({
  standalone: true,
  imports: [BackButtonComponent, PageTitleComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-back-button to="/sessions" label="Quay lại danh sách buổi học" />
    <app-page-title [title]="session()?.classSection ? session().classSection + ' · Buổi #' + sessionId : 'Buổi học #' + sessionId" [subtitle]="sessionSubtitle()">
      <span class="badge" [class.blue]="realtimeState() === 'ONLINE'" [class.warning]="realtimeState() === 'CONNECTING'" [class.gray]="realtimeState() === 'OFFLINE'">{{ realtimeLabel() }}</span>
      <button type="button" class="secondary" (click)="load()">Cập nhật</button>
      @if (session()?.canOperate && (session()?.status === 'READY' || session()?.status === 'DRAFT')) { <button type="button" (click)="requestAction('start')">Bắt đầu</button> }
      @if (session()?.canOperate && session()?.status === 'RUNNING') { <button type="button" class="danger" (click)="requestAction('stop')">Kết thúc</button> }
    </app-page-title>

    @if (error()) { <div class="error-box">{{ error() }}</div> }
    @if (sessionError()) { <div class="error-box">Buổi học: {{ sessionError() }}</div> }
    @if (dashboardError()) { <div class="error-box">Bảng theo dõi: {{ dashboardError() }}</div> }
    @if (message()) { <div class="success-box">{{ message() }}</div> }
    @if (loading()) { <div class="notice">Đang tải dữ liệu buổi học…</div> }

    @if (dashboard()) {
      <div class="stats stats-four">
        <section class="stat-card"><div class="stat-card-head"><div class="stat-icon">◎</div></div><span>Nhận diện hoạt động</span><strong>{{ dashboard().activeIdentities }}</strong><small>Định danh ổn định</small></section>
        <section class="stat-card"><div class="stat-card-head"><div class="stat-icon green">✓</div></div><span>Đã nhận diện</span><strong>{{ dashboard().identifiedStudents }}</strong><small>Sinh viên</small></section>
        <section class="stat-card"><div class="stat-card-head"><div class="stat-icon amber">?</div></div><span>Chưa xác định</span><strong>{{ dashboard().unknownIdentities }}</strong><small>Định danh chưa liên kết</small></section>
        <section class="stat-card"><div class="stat-card-head"><div class="stat-icon red">!</div></div><span>Cảnh báo</span><strong>{{ dashboard().recentAlerts?.length || 0 }}</strong><small>Gần đây</small></section>
      </div>

      <div class="dashboard-grid">
        <section class="card" style="min-height:360px">
          <div class="card-header" style="margin:-20px -20px 18px"><div><h3>Nguồn phân tích</h3><p>{{ session()?.cameraId ? 'Camera trực tiếp' : 'Video tải lên' }}</p></div><span class="badge" [class.warning]="dashboard().cameraHealth !== 'ONLINE'">{{ statusText(dashboard().cameraHealth) }}</span></div>
          <div class="empty-state"><div><div class="upload-icon">▶</div><strong>Luồng hình ảnh được xử lý bởi dịch vụ AI</strong><span>API hiện tại không cung cấp bản xem trước trực tiếp.</span></div></div>
        </section>

        <section class="card">
          <div class="card-header" style="margin:-20px -20px 18px"><div><h3>Phân tích AI</h3><p>Cập nhật {{ date(dashboard().generatedAt) }}</p></div><span class="badge purple">{{ statusText(dashboard().aiHealth) }}</span></div>
          @for (metric of behaviorMetrics(); track metric.label) {
            <div class="metric-row"><span>{{ metric.label }}</span><div class="progress-track"><div class="progress-value" [style.width.%]="metric.value" [style.background]="metric.color"></div></div><strong>{{ metric.value }}%</strong></div>
          }
          <div class="service-grid" style="grid-template-columns:1fr 1fr;margin-top:22px">
            <div class="service-card"><span class="status-dot" [style.background]="dashboard().modelReady ? '#16a34a' : '#dc2626'"></span><div><strong>Mô hình</strong><small>{{ dashboard().modelReady ? 'Sẵn sàng' : 'Chưa sẵn sàng' }}</small></div></div>
            <div class="service-card"><span class="status-dot" [style.background]="dashboard().aiHealth === 'ONLINE' ? '#16a34a' : '#dc2626'"></span><div><strong>Dịch vụ AI</strong><small>{{ statusText(dashboard().aiHealth) }}</small></div></div>
          </div>
        </section>
      </div>

      <section class="card table-card">
        <div class="card-header"><div><h3>Hành vi sinh viên theo thời gian thực</h3><p>Dữ liệu nhận diện và hành vi gần nhất</p></div></div>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Mã định danh</th><th>Luồng theo dõi</th><th>Sinh viên</th><th>Nhận diện</th><th>Hành vi hiện tại</th><th>Độ tin cậy</th><th>Lần cuối</th></tr></thead>
            <tbody>
              @for (row of dashboard().identities; track row.id) {
                <tr>
                  <td class="cell-title">{{ row.stableId }}</td><td>{{ row.currentTrackId || '—' }}</td><td>{{ row.studentId ? '#' + row.studentId : 'Chưa xác định' }}</td>
                  <td>{{ percent(row.identityConfidence) }}</td><td><span class="badge blue">{{ behaviorLabel(row.currentBehavior) }}</span></td>
                  <td>{{ percent(row.behaviorProbability) }}</td><td>{{ date(row.lastSeenAt) }}</td>
                </tr>
              } @empty { <tr><td colspan="7" class="empty">Chưa có identity đang hoạt động.</td></tr> }
            </tbody>
          </table>
        </div>
      </section>
    }

    @if (pendingAction()) {
      <app-modal [title]="actionTitle()" size="sm" [busy]="acting()" (close)="pendingAction.set(null)">
        <p>{{ actionMessage() }}</p>
        <div class="modal-footer" style="margin:20px -20px -20px">
          <button type="button" class="secondary" [disabled]="acting()" (click)="pendingAction.set(null)">Hủy</button>
          <button type="button" [class.danger]="pendingAction() === 'stop' || pendingAction() === 'cancel'" [disabled]="acting()" (click)="confirmAction()">Xác nhận</button>
        </div>
      </app-modal>
    }
  `,
})
export class SessionDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;
  readonly sessionId = Number(this.route.snapshot.paramMap.get('id'));
  readonly session = signal<any>(null);
  readonly dashboard = signal<any>(null);
  readonly error = signal('');
  readonly sessionError = signal('');
  readonly dashboardError = signal('');
  readonly message = signal('');
  readonly loading = signal(false);
  readonly acting = signal(false);
  readonly realtimeState = signal<'CONNECTING' | 'ONLINE' | 'OFFLINE'>('CONNECTING');
  readonly pendingAction = signal<'start' | 'stop' | 'cancel' | 'retry-finalize' | null>(null);
  readonly date = fmtDate;
  readonly statusText = statusText;

  ngOnInit(): void {
    this.load();
    void this.connectRealtime();
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    if (this.connection) {
      void this.connection.invoke('LeaveSession', this.sessionId).catch(() => undefined);
      void this.connection.stop();
    }
  }
  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.sessionError.set('');
    this.dashboardError.set('');
    forkJoin({
      session: this.api.session(this.sessionId).pipe(catchError((error) => { this.sessionError.set(errorText(error)); return of(null); })),
      dashboard: this.api.sessionDashboard(this.sessionId).pipe(catchError((error) => { this.dashboardError.set(errorText(error)); return of(null); })),
    }).subscribe({
      next: (result) => { this.session.set(result.session); this.dashboard.set(result.dashboard); this.loading.set(false); },
    });
  }
  requestAction(action: 'start' | 'stop' | 'cancel' | 'retry-finalize'): void { this.pendingAction.set(action); }
  confirmAction(): void {
    const action = this.pendingAction();
    if (!action) return;
    this.acting.set(true);
    this.api.sessionAction(this.sessionId, action).subscribe({
      next: () => { this.acting.set(false); this.pendingAction.set(null); this.message.set('Đã cập nhật trạng thái buổi học.'); this.load(); },
      error: (error) => { this.error.set(errorText(error)); this.acting.set(false); },
    });
  }
  sessionSubtitle(): string { const item = this.session(); return item ? `${item.course || ''} · ${item.teacher || ''} · ${fmtDate(item.scheduledStart)}` : 'Bảng theo dõi buổi học và kết quả phân tích'; }
  behaviorMetrics(): Array<{ label: string; value: number; color: string }> {
    const behavior = this.dashboard()?.behavior ?? {};
    return [
      { label: 'Tập trung', value: this.ratio(behavior.focused), color: '#16a34a' },
      { label: 'Mất tập trung', value: this.ratio(behavior.distracted), color: '#f59e0b' },
      { label: 'Buồn ngủ', value: this.ratio(behavior.sleepy), color: '#dc2626' },
      { label: 'Hoạt động', value: this.ratio(behavior.active), color: '#2563eb' },
    ];
  }
  ratio(value: unknown): number { return Math.round(Number(value ?? 0) * 100); }
  percent(value: unknown): string { return value == null ? '—' : `${this.ratio(value)}%`; }
  behaviorLabel(value: string | null | undefined): string { return behaviorText(value); }
  realtimeLabel(): string { return ({ CONNECTING: 'Đang kết nối realtime', ONLINE: 'Realtime trực tuyến', OFFLINE: 'Realtime gián đoạn' } as const)[this.realtimeState()]; }
  actionTitle(): string { return this.pendingAction() === 'start' ? 'Bắt đầu buổi học' : 'Kết thúc buổi học'; }
  actionMessage(): string { return this.pendingAction() === 'start' ? 'Bắt đầu luồng phân tích AI cho buổi học này?' : 'Kết thúc buổi học và tiến hành tổng hợp dữ liệu?'; }

  private async connectRealtime(): Promise<void> {
    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/session', { accessTokenFactory: () => this.auth.token() ?? '' })
      .withAutomaticReconnect()
      .build();

    this.connection.on('aiEvent', () => this.scheduleRealtimeRefresh());
    this.connection.on('sessionStatus', () => this.scheduleRealtimeRefresh());
    this.connection.onreconnecting(() => this.realtimeState.set('CONNECTING'));
    this.connection.onreconnected(async () => {
      this.realtimeState.set('ONLINE');
      await this.connection?.invoke('JoinSession', this.sessionId);
      this.scheduleRealtimeRefresh();
    });
    this.connection.onclose(() => this.realtimeState.set('OFFLINE'));

    try {
      await this.connection.start();
      await this.connection.invoke('JoinSession', this.sessionId);
      this.realtimeState.set('ONLINE');
    } catch {
      this.realtimeState.set('OFFLINE');
    }
  }

  private scheduleRealtimeRefresh(): void {
    if (this.refreshTimer) return;
    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = null;
      this.load();
    }, 500);
  }
}
