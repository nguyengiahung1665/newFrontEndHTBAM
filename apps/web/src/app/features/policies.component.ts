import { Component, OnInit, signal } from '@angular/core';
import { ApiService } from '../core/api.service';
import { AttendancePolicy } from '../core/models';
import { PageTitleComponent, errorText } from '../shared/ui';

@Component({
  standalone: true,
  imports: [PageTitleComponent],
  template: `
    <app-page-title
      title="Chuyên cần"
      subtitle="Theo dõi chính sách tính trạng thái có mặt, một phần và vắng mặt."
    />

    @if (error()) {
      <div class="error-box">{{ error() }}</div>
    }

    <div class="stats stats-three">
      <section class="stat-card">
        <span>Chính sách</span>
        <strong>{{ items().length }}</strong>
        <small>Đang cấu hình</small>
      </section>
      <section class="stat-card">
        <span>Ngưỡng có mặt</span>
        <strong>{{ items()[0]?.presentThreshold ?? '—' }}</strong>
        <small>Chính sách hiện hành</small>
      </section>
      <section class="stat-card">
        <span>Ngưỡng một phần</span>
        <strong>{{ items()[0]?.partialThreshold ?? '—' }}</strong>
        <small>Chính sách hiện hành</small>
      </section>
    </div>

    <section class="card table-card">
      <div class="card-header">
        <div>
          <h3>Chính sách chuyên cần</h3>
          <p>Giữ nguyên cấu hình nghiệp vụ từ API</p>
        </div>
      </div>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Mã</th><th>Tên</th><th>Ngưỡng có mặt</th><th>Ngưỡng một phần</th>
              <th>Điểm có mặt / một phần / vắng</th><th>Phiên bản</th>
            </tr>
          </thead>
          <tbody>
            @for (policy of items(); track policy.id) {
              <tr>
                <td class="cell-title">{{ policy.code }}</td>
                <td>{{ policy.name }}</td>
                <td>{{ policy.presentThreshold }}</td>
                <td>{{ policy.partialThreshold }}</td>
                <td>{{ policy.presentScore }} / {{ policy.partialScore }} / {{ policy.absentScore }}</td>
                <td><span class="badge blue">{{ policy.version }}</span></td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="empty">Chưa có chính sách chuyên cần.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  `,
})
export class PoliciesComponent implements OnInit {
  readonly items = signal<AttendancePolicy[]>([]);
  readonly error = signal('');

  constructor(private readonly api: ApiService) {}

  ngOnInit(): void {
    this.api.policies().subscribe({
      next: (items) => this.items.set(items),
      error: (error) => this.error.set(errorText(error)),
    });
  }
}
