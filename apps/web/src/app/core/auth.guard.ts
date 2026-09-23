import { inject } from '@angular/core';
import {
  CanActivateFn,
  Router,
} from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/login']);
  }

  return auth.ensureProfile().pipe(
    map(() => true),
    catchError((error) => {
      if (Number(error?.status) === 401) {
        auth.clear(false);
      }
      return of(router.createUrlTree(['/login']));
    }),
  );
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return true;
  }

  return auth.ensureProfile().pipe(
    map(() => router.createUrlTree(['/'])),
    catchError((error) => {
      if (Number(error?.status) === 401) {
        auth.clear(false);
      }
      return of(true);
    }),
  );
};

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/login']);
  }

  return auth.ensureProfile().pipe(
    map(() =>
      auth.isAdmin()
        ? true
        : router.createUrlTree(['/']),
    ),
    catchError((error) => {
      if (Number(error?.status) === 401) {
        auth.clear(false);
      }
      return of(router.createUrlTree(['/login']));
    }),
  );
};

export const academicGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/login']);
  }

  return auth.ensureProfile().pipe(
    map(() =>
      auth.isAdmin()
        ? router.createUrlTree([
            '/admin/users',
          ])
        : true,
    ),
    catchError((error) => {
      if (Number(error?.status) === 401) {
        auth.clear(false);
      }
      return of(
        router.createUrlTree([
          '/login',
        ]),
      );
    }),
  );
};

export const organizationGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/login']);
  }

  return auth.ensureProfile().pipe(
    map(() => {
      if (auth.isAdmin()) {
        return router.createUrlTree([
          '/admin/users',
        ]);
      }

      return auth.canManageStructure()
        ? true
        : router.createUrlTree(['/']);
    }),
    catchError((error) => {
      if (Number(error?.status) === 401) {
        auth.clear(false);
      }
      return of(
        router.createUrlTree([
          '/login',
        ]),
      );
    }),
  );
};
