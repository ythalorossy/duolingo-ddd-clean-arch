import { TestBed } from '@angular/core/testing';
import { CourseTreeComponent } from './course-tree.component';
import { Course } from '../../domain/course';

const courses: Course[] = [{ id: 'c1', title: 'Spanish', language: 'es',
  units: [{ id: 'u1', title: 'Basics', position: 1, lessons: [
    { id: 'l1', title: 'Greetings', position: 1, isLocked: false },
    { id: 'l2', title: 'Draft', position: 2, isLocked: true },
  ] }] }];

describe('CourseTreeComponent', () => {
  it('renders course, unit, and lesson titles and marks locked lessons', async () => {
    await TestBed.configureTestingModule({ imports: [CourseTreeComponent] }).compileComponents();
    const fixture = TestBed.createComponent(CourseTreeComponent);
    fixture.componentRef.setInput('courses', courses);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Spanish');
    expect(text).toContain('Basics');
    expect(text).toContain('Greetings');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.locked').length).toBe(1);
  });
});
