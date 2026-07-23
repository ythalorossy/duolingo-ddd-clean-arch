import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { CatalogDto } from '@duolingo/contracts';
import { Course } from '../domain/course';
import { mapCatalog } from './catalog.mapper';
import { API_BASE_URL } from './api-base-url.token';

@Injectable({ providedIn: 'root' })
export class CatalogRepository {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getCatalog(): Observable<Course[]> {
    return this.http.get<CatalogDto>(`${this.baseUrl}/courses`).pipe(
      map(mapCatalog),
      // Anti-corruption: the store never sees an HttpErrorResponse — only a domain-friendly Error.
      catchError(() => throwError(() => new Error('Unable to load the catalog.'))),
    );
  }
}
