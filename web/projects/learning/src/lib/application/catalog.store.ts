import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { CatalogRepository } from '../data/catalog.repository';
import { Course } from '../domain/course';

@Injectable({ providedIn: 'root' })
export class CatalogStore {
  private readonly repo = inject(CatalogRepository);

  private readonly _courses = signal<Course[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly courses: Signal<Course[]> = this._courses.asReadonly();
  readonly loading: Signal<boolean> = this._loading.asReadonly();
  readonly error: Signal<string | null> = this._error.asReadonly();
  readonly lessonCount = computed(() =>
    this._courses().reduce((n, c) => n + c.units.reduce((m, u) => m + u.lessons.length, 0), 0));

  /** Query use-case: load the catalog into signal state. */
  load(): void {
    this._loading.set(true);
    this._error.set(null);
    this.repo.getCatalog().subscribe({
      next: (courses) => { this._courses.set(courses); this._loading.set(false); },
      error: (e: Error) => { this._error.set(e.message); this._loading.set(false); },
    });
  }
}
