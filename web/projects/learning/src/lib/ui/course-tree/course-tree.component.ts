import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Course } from '../../domain/course';

@Component({
  selector: 'lib-course-tree',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (course of courses(); track course.id) {
      <section class="course">
        <h2>{{ course.title }}</h2>
        @for (unit of course.units; track unit.id) {
          <div class="unit">
            <h3>{{ unit.title }}</h3>
            <ul>
              @for (lesson of unit.lessons; track lesson.id) {
                <li [class.locked]="lesson.isLocked">{{ lesson.title }}</li>
              }
            </ul>
          </div>
        }
      </section>
    }
  `,
})
export class CourseTreeComponent {
  readonly courses = input.required<readonly Course[]>();
}
