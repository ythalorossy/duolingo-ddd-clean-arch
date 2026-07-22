import { InjectionToken } from '@angular/core';

/** Base URL of the backend API. Provided by the shell (composition root). */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
