import {
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import {
  ModalComponent,
  PageTitleComponent,
  errorText,
} from '../shared/ui';

type Tab =
  | 'faculties'
  | 'departments'
  | 'student-classes'
  | 'courses'
  | 'teachers'
  | 'class-sections'
  | 'rooms'
  | 'cameras'
  | 'roster';

@Component({
  standalone: true,
  imports: [
    FormsModule,
    ModalComponent,
    PageTitleComponent,
  ],
  template: `
    <app-page-title
      title="Danh mục & lớp học"
      subtitle="Quản lý dữ liệu nền, lớp học phần và roster sinh viên."
    >
      @if (
        auth.isAdmin() &&
        tab() !== 'roster'
      ) {
        <button
          type="button"
          (click)="openEditor()"
        >
          + Thêm {{ label() }}
        </button>
      }
    </app-page-title>

    @if (error()) {
      <div class="error-box">
        {{ error() }}
      </div>
    }

    @if (message()) {
      <div class="success-box">
        {{ message() }}
      </div>
    }

    <div class="tabs">
      @for (
        currentTab of tabs;
        track currentTab.key
      ) {
        <button
          type="button"
          [class.active]="
            tab() === currentTab.key
          "
          (click)="
            selectTab(
              currentTab.key
            )
          "
        >
          {{ currentTab.label }}
        </button>
      }
    </div>

    @if (tab() === 'roster') {
      <section class="card">
        <h3>Roster lớp học phần</h3>

        <div class="toolbar">
          <label>
            Lớp học phần

            <select
              [(ngModel)]="
                rosterSectionId
              "
              (ngModelChange)="
                loadRoster()
              "
            >
              <option [ngValue]="null">
                -- Chọn --
              </option>

              @for (
                section of refs.sections;
                track section.id
              ) {
                <option
                  [ngValue]="
                    section.id
                  "
                >
                  {{ section.code }}
                  -
                  {{ section.name }}
                </option>
              }
            </select>
          </label>
        </div>

        @if (rosterSectionId) {
          <div class="filter-bar" style="padding:0;border:0;margin-top:16px">
            <label class="grow">Tìm sinh viên<input [(ngModel)]="rosterQuery" placeholder="MSSV hoặc họ tên…" /></label>
            <button type="button" class="secondary" (click)="selectAllRoster()">Chọn tất cả</button>
            <button type="button" class="secondary" (click)="clearRoster()">Bỏ chọn tất cả</button>
          </div>
          <div class="roster-grid">
            <div class="roster-column">
              <h3>Sinh viên trong hệ thống ({{ availableRosterStudents().length }})</h3>
              @for (student of availableRosterStudents(); track student.id) {
                <label class="check"><input type="checkbox" [checked]="false" (change)="toggleRoster(student.id, $event)" />{{ student.studentCode }} - {{ student.fullName }}</label>
              } @empty { <p class="empty">Không còn sinh viên phù hợp.</p> }
            </div>
            <div class="roster-column">
              <h3>Sinh viên thuộc lớp ({{ selectedRosterStudents().length }})</h3>
              @for (student of selectedRosterStudents(); track student.id) {
                <label class="check"><input type="checkbox" [checked]="true" (change)="toggleRoster(student.id, $event)" />{{ student.studentCode }} - {{ student.fullName }}</label>
              } @empty { <p class="empty">Chưa chọn sinh viên.</p> }
            </div>
          </div>

          <button
            type="button"
            (click)="saveRoster()"
          >
            Lưu roster
          </button>
        }
      </section>
    } @else {
      <div class="filter-bar">
        <label class="grow">Tìm kiếm<input [(ngModel)]="managementSearch" [placeholder]="'Tìm trong ' + label().toLowerCase() + '…'" /></label>
      </div>
      <section class="card table-card">
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tên / Họ tên</th>
                <th>Thông tin</th>
                <th>Ghi chú</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>

            <tbody>
              @for (
                item of visibleData();
                track item.id
              ) {
                <tr>
                  <td>
                    {{
                      item.code ||
                        item.teacherCode
                    }}
                  </td>

                  <td>
                    {{
                      item.name ||
                        item.fullName
                    }}
                  </td>

                  <td>
                    {{ info(item) }}
                  </td>

                  <td>
                    <span
                      class="clamp"
                      [title]="
                        item.note || ''
                      "
                    >
                      {{
                        item.note || '—'
                      }}
                    </span>
                  </td>

                  <td>
                    <span
                      class="badge"
                      [class.inactive]="
                        item.isActive ===
                        false
                      "
                    >
                      {{
                        item.isActive ===
                        false
                          ? 'Ngừng hoạt động'
                          : item.status ||
                            'Hoạt động'
                      }}
                    </span>
                  </td>

                  <td>
                    @if (auth.isAdmin()) {
                      <div
                        class="inline-actions"
                      >
                        <button
                          type="button"
                          class="secondary small"
                          (click)="
                            openEditor(item)
                          "
                        >
                          Sửa
                        </button>

                        @if (
                          tab() ===
                          'cameras'
                        ) {
                          <button
                            type="button"
                            class="small"
                            (click)="
                              testCamera(
                                item
                              )
                            "
                          >
                            Test RTSP
                          </button>
                        }

                        @if (
                          item.isActive !==
                          false
                        ) {
                          <button
                            type="button"
                            class="danger small"
                            (click)="
                              askDeactivate(
                                item
                              )
                            "
                          >
                            Ngừng
                          </button>
                        }
                      </div>
                    }
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td
                    colspan="6"
                    class="empty"
                  >
                    Chưa có dữ liệu.
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    }

    @if (editorOpen()) {
      <app-modal
        [title]="
          (editingId()
            ? 'Sửa '
            : 'Thêm ') +
          label()
        "
        [busy]="saving()"
        [size]="tab() === 'class-sections' || tab() === 'cameras' ? 'lg' : 'md'"
        (close)="closeEditor()"
      >
        <form
          class="form-grid"
          (ngSubmit)="saveCatalog()"
        >
          <label>
            {{
              tab() === 'teachers'
                ? 'Mã GV'
                : 'Mã'
            }}
            *

            <input
              [(ngModel)]="
                form[
                  tab() === 'teachers'
                    ? 'teacherCode'
                    : 'code'
                ]
              "
              [ngModelOptions]="{
                standalone: true
              }"
              required
            />
          </label>

          <label>
            {{
              tab() === 'teachers'
                ? 'Họ tên'
                : 'Tên'
            }}
            *

            <input
              [(ngModel)]="
                form[
                  tab() === 'teachers'
                    ? 'fullName'
                    : 'name'
                ]
              "
              [ngModelOptions]="{
                standalone: true
              }"
              required
            />
          </label>

          @if (
            tab() === 'departments' ||
            tab() === 'student-classes'
          ) {
            <label>
              Khoa *

              <select
                [(ngModel)]="
                  form.facultyId
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              >
                <option
                  [ngValue]="null"
                >
                  -- Chọn --
                </option>

                @for (
                  faculty of
                    refs.faculties;
                  track faculty.id
                ) {
                  <option
                    [ngValue]="
                      faculty.id
                    "
                  >
                    {{ faculty.code }}
                    -
                    {{ faculty.name }}
                  </option>
                }
              </select>
            </label>
          }

          @if (
            tab() ===
            'student-classes'
          ) {
            <label>
              Năm bắt đầu

              <input
                type="number"
                [(ngModel)]="
                  form.startYear
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              />
            </label>
          }

          @if (
            tab() === 'courses' ||
            tab() === 'teachers'
          ) {
            <label>
              Bộ môn

              <select
                [(ngModel)]="
                  form.departmentId
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              >
                <option
                  [ngValue]="null"
                >
                  -- Chọn --
                </option>

                @for (
                  department of
                    refs.departments;
                  track department.id
                ) {
                  <option
                    [ngValue]="
                      department.id
                    "
                  >
                    {{
                      department.code
                    }}
                    -
                    {{
                      department.name
                    }}
                  </option>
                }
              </select>
            </label>
          }

          @if (
            tab() === 'courses'
          ) {
            <label>
              Tín chỉ

              <input
                type="number"
                min="1"
                [(ngModel)]="
                  form.credits
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              />
            </label>
          }

          @if (
            tab() === 'teachers'
          ) {
            <label>
              Email

              <input
                type="email"
                [(ngModel)]="
                  form.email
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              />
            </label>

            <label>
              User ID

              <input
                type="number"
                [(ngModel)]="
                  form.userId
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              />
            </label>
          }

          @if (
            tab() ===
            'class-sections'
          ) {
            <label>
              Môn học *

              <select
                [(ngModel)]="
                  form.courseId
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              >
                <option
                  [ngValue]="null"
                >
                  -- Chọn --
                </option>

                @for (
                  course of refs.courses;
                  track course.id
                ) {
                  <option
                    [ngValue]="
                      course.id
                    "
                  >
                    {{ course.code }}
                    -
                    {{ course.name }}
                  </option>
                }
              </select>
            </label>

            <label>
              Giảng viên *

              <select
                [(ngModel)]="
                  form.teacherId
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              >
                <option
                  [ngValue]="null"
                >
                  -- Chọn --
                </option>

                @for (
                  teacher of
                    refs.teachers;
                  track teacher.id
                ) {
                  <option
                    [ngValue]="
                      teacher.id
                    "
                  >
                    {{
                      teacher.teacherCode
                    }}
                    -
                    {{
                      teacher.fullName
                    }}
                  </option>
                }
              </select>
            </label>

            <label>
              Học kỳ *

              <input
                [(ngModel)]="
                  form.semester
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              />
            </label>

            <label>
              Năm học *

              <input
                [(ngModel)]="
                  form.academicYear
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              />
            </label>
          }

          @if (tab() === 'rooms') {
            <label>
              Vị trí

              <input
                [(ngModel)]="
                  form.location
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              />
            </label>

            <label>
              Sức chứa

              <input
                type="number"
                min="0"
                [(ngModel)]="
                  form.capacity
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              />
            </label>
          }

          @if (
            tab() === 'cameras'
          ) {
            <label>
              Phòng *

              <select
                [(ngModel)]="
                  form.roomId
                "
                [ngModelOptions]="{
                  standalone: true
                }"
              >
                <option
                  [ngValue]="null"
                >
                  -- Chọn --
                </option>

                @for (
                  room of refs.rooms;
                  track room.id
                ) {
                  <option
                    [ngValue]="
                      room.id
                    "
                  >
                    {{ room.code }}
                    -
                    {{ room.name }}
                  </option>
                }
              </select>
            </label>

            <label class="span2">
              RTSP URL

              <input
                [(ngModel)]="
                  form.rtspUrl
                "
                [ngModelOptions]="{
                  standalone: true
                }"
                placeholder="rtsp://..."
              />
            </label>
          }

          <label class="span2">
            Ghi chú

            <textarea
              rows="4"
              [(ngModel)]="form.note"
              [ngModelOptions]="{
                standalone: true
              }"
            ></textarea>
          </label>

          <label class="check">
            <input
              type="checkbox"
              [(ngModel)]="
                form.isActive
              "
              [ngModelOptions]="{
                standalone: true
              }"
            />
            Hoạt động
          </label>

          <div class="form-actions">
            <button
              type="submit"
              [disabled]="saving()"
            >
              {{
                saving()
                  ? 'Đang lưu…'
                  : 'Lưu'
              }}
            </button>

            <button
              type="button"
              class="secondary"
              [disabled]="saving()"
              (click)="closeEditor()"
            >
              Hủy
            </button>
          </div>
        </form>
      </app-modal>
    }

    @if (pendingDeactivate()) {
      <app-modal
        title="Xác nhận ngừng hoạt động"
        [busy]="saving()"
        (close)="
          cancelDeactivate()
        "
      >
        <p>
          Bạn có chắc muốn ngừng hoạt động
          <b>
            {{
              pendingDeactivate()
                ?.code ||
              pendingDeactivate()
                ?.teacherCode
            }}
          </b>
          không?
        </p>

        <div class="form-actions">
          <button
            type="button"
            class="danger"
            [disabled]="saving()"
            (click)="
              confirmDeactivate()
            "
          >
            Xác nhận
          </button>

          <button
            type="button"
            class="secondary"
            [disabled]="saving()"
            (click)="
              cancelDeactivate()
            "
          >
            Hủy
          </button>
        </div>
      </app-modal>
    }
  `,
})
export class ManagementComponent
  implements OnInit
{
  private readonly api =
    inject(ApiService);

  readonly auth =
    inject(AuthService);

  readonly tabs: {
    key: Tab;
    label: string;
  }[] = [
    {
      key: 'faculties',
      label: 'Khoa',
    },
    {
      key: 'departments',
      label: 'Bộ môn',
    },
    {
      key: 'student-classes',
      label: 'Lớp sinh hoạt',
    },
    {
      key: 'courses',
      label: 'Môn học',
    },
    {
      key: 'teachers',
      label: 'Giảng viên',
    },
    {
      key: 'class-sections',
      label: 'Lớp học phần',
    },
    {
      key: 'rooms',
      label: 'Phòng học',
    },
    {
      key: 'cameras',
      label: 'Camera',
    },
    {
      key: 'roster',
      label: 'Roster lớp học phần',
    },
  ];

  readonly tab =
    signal<Tab>('faculties');

  readonly data =
    signal<any[]>([]);

  readonly error = signal('');
  readonly message = signal('');

  readonly editorOpen =
    signal(false);

  readonly editingId =
    signal<number | null>(null);

  readonly saving =
    signal(false);

  readonly pendingDeactivate =
    signal<any | null>(null);

  form: any = {
    isActive: true,
    note: '',
  };

  refs: any = {
    faculties: [],
    departments: [],
    classes: [],
    courses: [],
    teachers: [],
    sections: [],
    rooms: [],
    cameras: [],
    students: [],
  };

  rosterSectionId:
    | number
    | null = null;

  managementSearch = '';
  rosterQuery = '';

  readonly rosterIds =
    signal(new Set<number>());

  visibleData(): any[] {
    const key = this.managementSearch.trim().toLowerCase();
    if (!key) return this.data();
    return this.data().filter((item) =>
      [item.code, item.teacherCode, item.name, item.fullName, item.email, item.note]
        .filter(Boolean)
        .some((value) => String(value).toLowerCase().includes(key)),
    );
  }

  availableRosterStudents(): any[] {
    const key = this.rosterQuery.trim().toLowerCase();
    return this.refs.students.filter((student: any) =>
      !this.rosterIds().has(student.id) &&
      (!key || `${student.studentCode} ${student.fullName}`.toLowerCase().includes(key)),
    );
  }

  selectedRosterStudents(): any[] {
    const key = this.rosterQuery.trim().toLowerCase();
    return this.refs.students.filter((student: any) =>
      this.rosterIds().has(student.id) &&
      (!key || `${student.studentCode} ${student.fullName}`.toLowerCase().includes(key)),
    );
  }

  selectAllRoster(): void {
    this.rosterIds.set(new Set(this.refs.students.map((student: any) => student.id)));
  }

  clearRoster(): void {
    this.rosterIds.set(new Set<number>());
  }

  ngOnInit(): void {
    this.loadAll();
  }

  label(): string {
    return (
      this.tabs.find(
        (item) =>
          item.key === this.tab(),
      )?.label ?? ''
    );
  }

  selectTab(tab: Tab): void {
    this.tab.set(tab);
    this.editorOpen.set(false);
    this.pendingDeactivate.set(null);
    this.refreshData();

    if (
      tab === 'roster' &&
      !this.refs.students.length
    ) {
      this.api
        .students('', 'active')
        .subscribe((students) => {
          this.refs.students =
            students;
        });
    }
  }

  loadAll(): void {
    this.error.set('');

    forkJoin({
      faculties:
        this.api.faculties(),

      departments:
        this.api.departments(),

      classes:
        this.api.studentClasses(),

      courses:
        this.api.courses(),

      teachers:
        this.api.teachers(),

      sections:
        this.api.classSections(),

      rooms:
        this.api.rooms(),

      cameras:
        this.api.cameras(),

      students:
        this.api.students(
          '',
          'active',
        ),
    }).subscribe({
      next: (result) => {
        this.refs = {
          faculties:
            result.faculties,

          departments:
            result.departments,

          classes:
            result.classes,

          courses:
            result.courses,

          teachers:
            result.teachers,

          sections:
            result.sections,

          rooms:
            result.rooms,

          cameras:
            result.cameras,

          students:
            result.students,
        };

        this.refreshData();
      },

      error: (error) =>
        this.error.set(
          errorText(error),
        ),
    });
  }

  refreshData(): void {
    const map: Record<
      string,
      any[]
    > = {
      faculties:
        this.refs.faculties,

      departments:
        this.refs.departments,

      'student-classes':
        this.refs.classes,

      courses:
        this.refs.courses,

      teachers:
        this.refs.teachers,

      'class-sections':
        this.refs.sections,

      rooms:
        this.refs.rooms,

      cameras:
        this.refs.cameras,
    };

    this.data.set(
      map[this.tab()] ?? [],
    );
  }

  openEditor(item?: any): void {
    this.error.set('');
    this.message.set('');

    this.editingId.set(
      item?.id ?? null,
    );

    this.form = item
      ? {
          ...item,
        }
      : this.emptyForm();

    this.editorOpen.set(true);
  }

  closeEditor(): void {
    if (!this.saving()) {
      this.editorOpen.set(false);
    }
  }

  emptyForm(): any {
    return {
      isActive: true,
      note: '',
      credits: 3,
      capacity: 0,
      facultyId: null,
      departmentId: null,
      courseId: null,
      teacherId: null,
      roomId: null,
      semester: '',
      academicYear: '',
    };
  }

  saveCatalog(): void {
    const validationError =
      this.validateCatalog();

    if (validationError) {
      this.error.set(
        validationError,
      );
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.message.set('');

    this.api
      .saveCatalog(
        this.tab(),
        this.editingId(),
        this.form,
      )
      .subscribe({
        next: () => {
          this.message.set(
            `Đã lưu ${this.label()}.`,
          );

          this.editorOpen.set(
            false,
          );

          this.saving.set(false);
          this.loadAll();
        },

        error: (error) => {
          this.error.set(
            errorText(error),
          );

          this.saving.set(false);
        },
      });
  }

  validateCatalog(): string | null {
    const isTeacher =
      this.tab() === 'teachers';

    const code = String(
      isTeacher
        ? this.form.teacherCode ?? ''
        : this.form.code ?? '',
    ).trim();

    const name = String(
      isTeacher
        ? this.form.fullName ?? ''
        : this.form.name ?? '',
    ).trim();

    if (!code) {
      return 'Mã là bắt buộc.';
    }

    if (!name) {
      return 'Tên/Họ tên là bắt buộc.';
    }

    if (
      (
        this.tab() ===
          'departments' ||
        this.tab() ===
          'student-classes'
      ) &&
      !this.form.facultyId
    ) {
      return 'Vui lòng chọn Khoa.';
    }

    if (
      this.tab() ===
        'class-sections'
    ) {
      if (!this.form.courseId) {
        return 'Vui lòng chọn Môn học.';
      }

      if (!this.form.teacherId) {
        return 'Vui lòng chọn Giảng viên.';
      }

      if (
        !String(
          this.form.semester ??
            '',
        ).trim()
      ) {
        return 'Học kỳ là bắt buộc.';
      }

      if (
        !String(
          this.form.academicYear ??
            '',
        ).trim()
      ) {
        return 'Năm học là bắt buộc.';
      }
    }

    if (
      this.tab() === 'rooms' &&
      Number(this.form.capacity) < 0
    ) {
      return 'Sức chứa phải lớn hơn hoặc bằng 0.';
    }

    if (
      this.tab() === 'cameras' &&
      !this.form.roomId
    ) {
      return 'Vui lòng chọn Phòng.';
    }

    return null;
  }

  askDeactivate(item: any): void {
    this.pendingDeactivate.set(
      item,
    );
  }

  cancelDeactivate(): void {
    if (!this.saving()) {
      this.pendingDeactivate.set(
        null,
      );
    }
  }

  confirmDeactivate(): void {
    const item =
      this.pendingDeactivate();

    if (!item) {
      return;
    }

    this.saving.set(true);

    this.api
      .deactivateCatalog(
        this.tab(),
        item.id,
      )
      .subscribe({
        next: () => {
          this.pendingDeactivate.set(
            null,
          );

          this.saving.set(false);

          this.message.set(
            `Đã ngừng hoạt động ${this.label()}.`,
          );

          this.loadAll();
        },

        error: (error) => {
          this.error.set(
            errorText(error),
          );

          this.saving.set(false);
        },
      });
  }

  testCamera(item: any): void {
    this.api
      .testCamera(item.id)
      .subscribe({
        next: (result) => {
          this.message.set(
            `Camera ${item.code}: ${result.status} — ${result.lastHealthMessage ?? ''}`,
          );

          this.loadAll();
        },

        error: (error) =>
          this.error.set(
            errorText(error),
          ),
      });
  }

  info(item: any): string {
    switch (this.tab()) {
      case 'departments':
        return item.faculty ?? '—';

      case 'student-classes':
        return `${item.faculty ?? ''} ${
          item.startYear
            ? '• ' + item.startYear
            : ''
        }`;

      case 'courses':
        return `${item.credits ?? 0} tín chỉ`;

      case 'teachers':
        return item.email ?? '—';

      case 'class-sections':
        return `${item.course ?? ''} • ${item.teacher ?? ''} • ${item.semester ?? ''} ${item.academicYear ?? ''}`;

      case 'rooms':
        return `${item.location ?? ''} • ${item.capacity ?? 0} chỗ`;

      case 'cameras':
        return `${item.room ?? ''} • ${item.lastHealthMessage ?? 'Chưa test'}`;

      default:
        return '—';
    }
  }

  loadRoster(): void {
    if (!this.rosterSectionId) {
      this.rosterIds.set(
        new Set<number>(),
      );
      return;
    }

    this.api
      .roster(
        this.rosterSectionId,
      )
      .subscribe((rows) => {
        this.rosterIds.set(
          new Set(
            rows.map(
              (row) =>
                row.studentId,
            ),
          ),
        );
      });
  }

  toggleRoster(
    studentId: number,
    event: Event,
  ): void {
    const selected =
      new Set(this.rosterIds());

    const checked =
      (
        event.target as HTMLInputElement
      ).checked;

    if (checked) {
      selected.add(studentId);
    } else {
      selected.delete(studentId);
    }

    this.rosterIds.set(selected);
  }

  saveRoster(): void {
    if (!this.rosterSectionId) {
      return;
    }

    this.api
      .setRoster(
        this.rosterSectionId,
        [...this.rosterIds()],
      )
      .subscribe({
        next: () =>
          this.message.set(
            'Đã lưu roster.',
          ),

        error: (error) =>
          this.error.set(
            errorText(error),
          ),
      });
  }
}
