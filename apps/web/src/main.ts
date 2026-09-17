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

    const pre = document.createElement('pre');
    pre.style.whiteSpace = 'pre-wrap';
    pre.style.background = '#fee';
    pre.style.padding = '16px';
    pre.style.borderRadius = '8px';

    pre.textContent =
      error instanceof Error
        ? `${error.name}: ${error.message}\n${error.stack ?? ''}`
        : String(error);

    main.append(title, description, pre);
    document.body.append(main);
  },
);
