import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CatalogStore } from './catalog.store';
import { CatalogRepository } from '../data/catalog.repository';
import { Course } from '../domain/course';

function fakeCourses(): Course[] {
  return [{ id: 'c1', title: 'Spanish', language: 'es',
    units: [{ id: 'u1', title: 'Basics', position: 1,
      lessons: [{ id: 'l1', title: 'Greetings', position: 1, isLocked: false }] }] }];
}

describe('CatalogStore', () => {
  function setup(repo: Partial<CatalogRepository>) {
    TestBed.configureTestingModule({
      providers: [CatalogStore, { provide: CatalogRepository, useValue: repo }],
    });
    return TestBed.inject(CatalogStore);
  }

  it('load() populates courses and clears loading on success', () => {
    const store = setup({ getCatalog: () => of(fakeCourses()) });
    store.load();
    expect(store.loading()).toBe(false);
    expect(store.courses().length).toBe(1);
    expect(store.error()).toBeNull();
    expect(store.lessonCount()).toBe(1);
  });

  it('load() sets a friendly error message on failure', () => {
    const store = setup({ getCatalog: () => throwError(() => new Error('Unable to load the catalog.')) });
    store.load();
    expect(store.loading()).toBe(false);
    expect(store.error()).toBe('Unable to load the catalog.');
    expect(store.courses()).toEqual([]);
  });
});
