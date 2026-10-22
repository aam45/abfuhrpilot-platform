import { ApplicationConfig, LOCALE_ID, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeDe from '@angular/common/locales/de';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { idempotencyInterceptor } from './core/interceptors/idempotency.interceptor';

// German UI end-to-end: date/number pipes must speak German too
// (otherwise `date:'EEEE'` renders "Thursday" in a German console).
registerLocaleData(localeDe);

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: LOCALE_ID, useValue: 'de-DE' },
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    // idempotency first: the key is attached before auth, so the 401→refresh
    // replay reuses the SAME key for the same action.
    provideHttpClient(withInterceptors([idempotencyInterceptor, authInterceptor])),
  ],
};
