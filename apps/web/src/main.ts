import 'zone.js';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

bootstrapApplication(AppComponent, appConfig).catch(
  (error: unknown) => {
    console.error('HTBAM bootstrap failed', error);

    document.body.replaceChildren();

    const main = document.createElement('main');
    main.style.fontFamily = 'Arial, sans-serif';
    main.style.padding = '32px';
    main.style.maxWidth = '900px';
    main.style.margin = 'auto';

    const title = document.createElement('h1');
    title.textContent =
      'Không thể khởi động giao diện HTBAM';

    const description = document.createElement('p');
    description.textContent =
      'Frontend gặp lỗi khi khởi tạo. Hãy xem chi tiết bên dưới.';

    const notice = document.createElement('p');
    notice.style.background = '#fee';
    notice.style.padding = '16px';
    notice.style.borderRadius = '8px';
    notice.textContent =
      'Không thể tải giao diện. Vui lòng tải lại trang hoặc liên hệ quản trị hệ thống.';

    main.append(title, description, notice);
    document.body.append(main);
  },
);
