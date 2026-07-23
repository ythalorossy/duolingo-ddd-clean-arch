import { CatalogDto, CourseDto, LessonDto, UnitDto } from './public-api';

describe('contracts public API', () => {
  it('exposes catalog DTO shapes matching the backend', () => {
    const lesson: LessonDto = { id: 'l1', title: 'Greetings', position: 1, isPublished: true };
    const unit: UnitDto = { id: 'u1', title: 'Basics', position: 1, lessons: [lesson] };
    const course: CourseDto = { id: 'c1', title: 'Spanish', language: 'es', units: [unit] };
    const catalog: CatalogDto = { courses: [course] };

    expect(catalog.courses[0].units[0].lessons[0].isPublished).toBe(true);
    // language is nullable, mirroring string? on the backend
    const noLanguage: CourseDto = { id: 'c2', title: 'X', language: null, units: [] };
    expect(noLanguage.language).toBeNull();
  });
});
