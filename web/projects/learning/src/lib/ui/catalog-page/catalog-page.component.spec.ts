import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { CatalogPageComponent } from './catalog-page.component';
import { CatalogStore } from '../../application/catalog.store';
import { Course } from '../../domain/course';

class FakeStore {
  courses = signal<Course[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);
  lessonCount = signal(0);
  load = vi.fn();
}

describe('CatalogPageComponent', () => {
  function render(store: FakeStore) {
    TestBed.configureTestingModule({
      imports: [CatalogPageComponent],
      providers: [{ provide: CatalogStore, useValue: store }],
    });
    const fixture = TestBed.createComponent(CatalogPageComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('calls load() on init', () => {
    const store = new FakeStore();
    render(store);
    expect(store.load).toHaveBeenCalled();
  });

  it('shows a loading indicator while loading', () => {
    const store = new FakeStore();
    store.loading.set(true);
    const fixture = render(store);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Loading');
  });

  it('shows the error with a retry that re-invokes load()', () => {
    const store = new FakeStore();
    store.error.set('Unable to load the catalog.');
    const fixture = render(store);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Unable to load the catalog.');
    el.querySelector('button')!.dispatchEvent(new Event('click'));
    expect(store.load).toHaveBeenCalledTimes(2); // once on init, once on retry
  });
});
