import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CatalogRepository } from './catalog.repository';
import { API_BASE_URL } from './api-base-url.token';
import { Course } from '../domain/course';

describe('CatalogRepository', () => {
  let repo: CatalogRepository;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://test.local' },
        CatalogRepository,
      ],
    });
    repo = TestBed.inject(CatalogRepository);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('GETs /courses and maps to domain courses', () => {
    let result: Course[] | undefined;
    repo.getCatalog().subscribe((c) => (result = c));

    const req = http.expectOne('http://test.local/courses');
    expect(req.request.method).toBe('GET');
    req.flush({
      courses: [{ id: 'c1', title: 'Spanish', language: 'es',
        units: [{ id: 'u1', title: 'Basics', position: 1,
          lessons: [{ id: 'l1', title: 'Greetings', position: 1, isPublished: true }] }] }],
    });

    expect(result![0].units[0].lessons[0].isLocked).toBe(false);
  });

  it('translates an HTTP error into a domain-friendly error', () => {
    let caught: Error | undefined;
    repo.getCatalog().subscribe({ error: (e: unknown) => (caught = e as Error) });

    http.expectOne('http://test.local/courses').flush('boom',
      { status: 500, statusText: 'Server Error' });

    expect(caught).toBeInstanceOf(Error);
    expect(caught!.message).toBe('Unable to load the catalog.');
  });
});
