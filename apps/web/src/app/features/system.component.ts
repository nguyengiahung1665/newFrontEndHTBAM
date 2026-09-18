import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { catchError, forkJoin, of } from 'rxjs';
import { ApiService } from '../core/api.service';
import { Capabilities } from '../core/models';
import { PageTitleComponent, errorText } from '../shared/ui';

@Component({
  standalone: true,
  imports: [PageTitleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-title title="Trang thai he thong" subtitle="Theo doi suc khoe cac thanh phan cua HTBAM.">
      <button type="button" class="secondary" (click)="load()">Cap nhat</button>
    </app-page-title>
    @if (error()) { <div class="error-box">{{ error() }}</div> }
    @if (capabilitiesError()) { <div class="error-box">Capabilities: {{ capabilitiesError() }}</div> }
    @if (countsError()) { <div class="error-box">Counts: {{ countsError() }}</div> }
    <div class="service-grid">
      @for (service of services(); track service.name) {
        <section class="card" style="margin:0">
          <div class="service-card" style="padding:0;border:0">
            <span class="status-dot" [style.background]="service.ok ? '#16a34a' : '#dc2626'"></span>
            <div><strong>{{ service.name }}</strong><small>{{ service.description }}</small></div>
          </div>
          <div style="margin-top:14px"><span class="badge" [class.danger]="!service.ok">{{ service.ok ? 'Hoat dong' : 'Ngoai tuyen' }}</span></div>
        </section>
      } @empty {
        <section class="card"><div class="empty-state"><div><strong>Dang kiem tra dich vu</strong><span>Vui long cho phan hoi tu he thong.</span></div></div></section>
      }
    </div>
    <section class="card" style="margin-top:18px">
      <div class="card-header" style="margin:-20px -20px 18px"><div><h3>Nang luc AI</h3><p>Thong tin do API capabilities cung cap</p></div></div>
      <div class="kv">
        <span>Session inference</span><b>{{ caps()?.sessionInference ? 'San sang' : 'Chua san sang' }}</b>
        <span>Face enrollment</span><b>{{ caps()?.faceEnrollment ? 'San sang' : 'Chua san sang' }}</b>
        <span>LLM Provider</span><b>{{ caps()?.llmProvider || 'Chua cau hinh' }}</b>
        <span>Loaded models</span><b>{{ caps()?.loadedModels?.join(', ') || '-' }}</b>
      </div>
    </section>
  `,
})
export class SystemComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly caps = signal<Capabilities | null>(null);
  readonly counts = signal<any>(null);
  readonly error = signal('');
  readonly capabilitiesError = signal('');
  readonly countsError = signal('');

  ngOnInit(): void { this.load(); }

  load(): void {
    this.error.set('');
    this.capabilitiesError.set('');
    this.countsError.set('');
    forkJoin({
      caps: this.api.capabilities().pipe(catchError((error) => { this.capabilitiesError.set(errorText(error)); return of(null as Capabilities | null); })),
      counts: this.api.counts().pipe(catchError((error) => { this.countsError.set(errorText(error)); return of(null); })),
    }).subscribe({
      next: (result) => { this.caps.set(result.caps); this.counts.set(result.counts); },
      error: (error) => this.error.set(errorText(error)),
    });
  }

  services(): Array<{name: string; description: string; ok: boolean}> {
    const c = this.caps();
    if (!c) return [];
    return [
      { name: 'Backend', description: 'ASP.NET Core API', ok: c.backend },
      { name: 'Database', description: 'SQL Server', ok: c.database },
      { name: 'MinIO', description: 'Object Storage', ok: c.objectStorage },
      { name: 'AI Service', description: 'FastAPI va mo hinh AI', ok: c.aiHealth },
      { name: 'Face Recognition', description: 'Dang ky va nhan dien khuon mat', ok: c.faceEnrollment },
      { name: 'LLM Provider', description: c.llmProvider || 'Chua cau hinh', ok: c.llmProvider !== 'NOT_CONFIGURED' },
    ];
  }
}
