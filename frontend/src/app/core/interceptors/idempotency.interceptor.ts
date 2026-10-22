import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Adds a fresh `Idempotency-Key` (UUID) to every mutating API call so the
 * backend can safely deduplicate retries (driver actions especially). Rules:
 *  - only POST/PUT/PATCH/DELETE — never GET;
 *  - auth/token endpoints are excluded (login/refresh/logout/reset/verify);
 *  - a key already present is kept — this is what makes the 401→refresh replay
 *    in the auth interceptor reuse the SAME key for the same action, while two
 *    separate user actions always get different keys.
 */
export const idempotencyInterceptor: HttpInterceptorFn = (req, next) => {
  const mutating = ['POST', 'PUT', 'PATCH', 'DELETE'].includes(req.method);
  const isAuthCall = req.url.includes('/auth/');
  if (!mutating || isAuthCall || req.headers.has('Idempotency-Key')) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { 'Idempotency-Key': newIdempotencyKey() } }));
};

export function newIdempotencyKey(): string {
  // crypto.randomUUID is available in all modern browsers; fall back defensively.
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now().toString(16)}-${Math.random().toString(16).slice(2, 10)}-${Math.random().toString(16).slice(2, 10)}`;
}
