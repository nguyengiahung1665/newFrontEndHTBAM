import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { LoginResponse, UserInfo } from './models';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly tokenKey = 'htbam_token';
  private readonly userKey = 'htbam_user';

  private readonly tokenState = signal<string | null>(
    this.readInitialToken(),
  );

  readonly user = signal<Partial<UserInfo>>(
    this.readUser(),
  );

  readonly isLoggedIn = computed(
    () => !!this.tokenState(),
  );

  readonly isAdmin = computed(
    () => this.user().roles?.includes('ADMIN') ?? false,
  );

  token(): string | null {
    return this.tokenState();
  }

  login(
    userName: string,
    password: string,
  ): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>('/api/auth/login', {
        userName,
        password,
      })
      .pipe(
        tap((response) => {
          sessionStorage.setItem(
            this.tokenKey,
            response.accessToken,
          );

          this.tokenState.set(
            response.accessToken,
          );

          const currentUser: Partial<UserInfo> = {
            id: response.userId,
            userName,
            email: '',
            fullName: response.fullName,
            status: 'ACTIVE',
            roles: response.roles,
          };

          sessionStorage.setItem(
            this.userKey,
            JSON.stringify(currentUser),
          );

          this.user.set(currentUser);
        }),
      );
  }

  me(): Observable<UserInfo> {
    return this.http.get<UserInfo>('/api/auth/me').pipe(
      tap((currentUser) => {
        this.user.set(currentUser);

        sessionStorage.setItem(
          this.userKey,
          JSON.stringify(currentUser),
        );
      }),
    );
  }

  changePassword(
    currentPassword: string,
    newPassword: string,
  ): Observable<unknown> {
    return this.http.put('/api/auth/change-password', {
      currentPassword,
      newPassword,
    });
  }

  logout(): void {
    this.http.post('/api/auth/logout', {}).subscribe({
      complete: () => this.clear(),
      error: () => this.clear(),
    });
  }

  clear(navigateToLogin = true): void {
    sessionStorage.removeItem(this.tokenKey);
    sessionStorage.removeItem(this.userKey);

    // Dọn token cũ của các bản frontend trước để tránh tự vào Dashboard.
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
    localStorage.removeItem('htbam_auth_version');

    this.tokenState.set(null);
    this.user.set({});

    if (navigateToLogin) {
      void this.router.navigateByUrl('/login');
    }
  }

  private readInitialToken(): string | null {
    // Chỉ sessionStorage được dùng cho bản Angular hiện tại.
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
    localStorage.removeItem('htbam_auth_version');

    const token = sessionStorage.getItem(this.tokenKey);

    if (!token) {
      return null;
    }

    if (this.isJwtExpired(token)) {
      sessionStorage.removeItem(this.tokenKey);
      sessionStorage.removeItem(this.userKey);
      return null;
    }

    return token;
  }

  private readUser(): Partial<UserInfo> {
    try {
      return JSON.parse(
        sessionStorage.getItem(this.userKey) || '{}',
      ) as Partial<UserInfo>;
    } catch {
      return {};
    }
  }

  private isJwtExpired(token: string): boolean {
    try {
      const payload = token.split('.')[1];

      if (!payload) {
        return true;
      }

      const normalized = payload
        .replace(/-/g, '+')
        .replace(/_/g, '/');

      const decoded = JSON.parse(
        decodeURIComponent(
          Array.prototype.map
            .call(
              atob(normalized),
              (character: string) =>
                '%' +
                (
                  '00' +
                  character
                    .charCodeAt(0)
                    .toString(16)
                ).slice(-2),
            )
            .join(''),
        ),
      );

      if (!decoded.exp) {
        return false;
      }

      return Date.now() >= decoded.exp * 1000;
    } catch {
      return true;
    }
  }
}
