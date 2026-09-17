import {
  HttpErrorResponse,
  HttpInterceptorFn,
} from '@angular/common/http';
import { inject } from '@angular/core';
import {
  catchError,
  throwError,
} from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (
  request,
  next,
) => {
  const auth = inject(AuthService);
  const token = auth.token();

  const authenticatedRequest = token
    ? request.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`,
        },
      })
    : request;

  return next(authenticatedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      const isLoginRequest =
        request.url.includes('/api/auth/login');

      if (
        error.status === 401 &&
        !isLoginRequest
      ) {
        auth.clear();
      }

      // 502/503/504 không logout.
      // Component sẽ hiển thị backend chưa sẵn sàng.
      return throwError(() => error);
    }),
  );
};
