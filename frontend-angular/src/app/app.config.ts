import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // withFetch() uses the browser Fetch API instead of XMLHttpRequest —
    // required for Angular's SSR compatibility and is the modern default.
    provideHttpClient(withFetch()),
  ]
};
