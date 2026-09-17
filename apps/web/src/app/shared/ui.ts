import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  Output,
  ViewChild,
} from '@angular/core';

@Component({
  selector: 'app-page-title',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-title">
      <div>
        <h1>{{ title }}</h1>

        @if (subtitle) {
          <p>{{ subtitle }}</p>
        }
      </div>

      <div class="actions">
        <ng-content />
      </div>
    </div>
  `,
})
export class PageTitleComponent {
  @Input({ required: true })
  title = '';

  @Input()
  subtitle = '';
}

@Component({
  selector: 'app-modal',
  standalone: true,
  template: `
    <div
      class="modal-backdrop"
      (mousedown)="onBackdrop($event)"
    >
      <section
        #dialog
        class="modal-dialog"
        [class.modal-sm]="size === 'sm'"
        [class.modal-lg]="size === 'lg'"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="title"
        tabindex="-1"
      >
        <header>
          <h3>{{ title }}</h3>

          <button
            type="button"
            class="modal-close"
            [disabled]="busy"
            (click)="requestClose()"
            aria-label="Đóng"
          >
            ×
          </button>
        </header>

        <div class="modal-body">
          <ng-content />
        </div>
      </section>
    </div>
  `,
})
export class ModalComponent implements AfterViewInit {
  @Input()
  title = '';

  @Input()
  busy = false;

  @Input()
  size: 'sm' | 'md' | 'lg' = 'md';

  @Output()
  close = new EventEmitter<void>();

  @ViewChild('dialog')
  private dialog?: ElementRef<HTMLElement>;

  ngAfterViewInit(): void {
    setTimeout(() => {
      this.dialog?.nativeElement.focus();
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.requestClose();
  }

  onBackdrop(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.requestClose();
    }
  }

  requestClose(): void {
    if (!this.busy) {
      this.close.emit();
    }
  }
}

export function errorText(error: any): string {
  const status = Number(error?.status ?? 0);

  if ([502, 503, 504].includes(status)) {
    return 'Backend HTBAM chưa sẵn sàng. Vui lòng thử lại sau.';
  }

  if (
    typeof error?.error === 'string' &&
    /<html|bad gateway|nginx/i.test(error.error)
  ) {
    return 'Backend HTBAM chưa sẵn sàng. Vui lòng thử lại sau.';
  }

  return (
    error?.error?.message ??
    error?.error?.title ??
    error?.message ??
    'Đã xảy ra lỗi.'
  );
}

export function fmtDate(
  value?: string | null,
): string {
  return value
    ? new Date(value).toLocaleString('vi-VN')
    : '—';
}
