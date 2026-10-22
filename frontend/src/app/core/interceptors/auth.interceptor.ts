import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

/**
 * Attaches the JWT and, on a 401, transparently tries a single token refresh
 * and replays the request. If refresh fails (or the failing call was itself an
 * auth endpoint), the user is logged out.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const isAuthCall = req.url.includes('/auth/');
  const token = auth.token;
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((err) => {
      if (err?.status !== 401 || isAuthCall) return throwError(() => err);

      return auth.refresh().pipe(
        switchMap((newToken) => {
          if (!newToken) { auth.logout(); return throwError(() => err); }
          return next(req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } }));
        }),
      );
    }),
  );
};
