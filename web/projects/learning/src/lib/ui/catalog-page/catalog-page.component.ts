import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { CatalogStore } from '../../application/catalog.store';
import { CourseTreeComponent } from '../course-tree/course-tree.component';

@Component({
  selector: 'lib-catalog-page',
  standalone: true,
  imports: [CourseTreeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (store.loading()) {
      <p class="loading">Loading catalog…</p>
    } @else if (store.error(); as message) {
      <div class="error">
        <p>{{ message }}</p>
        <button type="button" (click)="store.load()">Retry</button>
      </div>
    } @else if (store.courses().length === 0) {
      <p class="empty">No courses yet.</p>
    } @else {
      <lib-course-tree [courses]="store.courses()" />
    }
  `,
})
export class CatalogPageComponent implements OnInit {
  readonly store = inject(CatalogStore);
  ngOnInit(): void { this.store.load(); }
}
