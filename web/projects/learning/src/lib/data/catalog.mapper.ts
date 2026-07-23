// Anti-corruption seam: the ONLY place @duolingo/contracts DTOs are read. Wire types never
// leak past this file — the rest of the library speaks the domain model in ./domain/course.
import { CatalogDto, CourseDto, LessonDto, UnitDto } from '@duolingo/contracts';
import { Course, Lesson, Unit } from '../domain/course';

export function mapCatalog(dto: CatalogDto): Course[] {
  return dto.courses.map(mapCourse);
}

function mapCourse(dto: CourseDto): Course {
  return { id: dto.id, title: dto.title, language: dto.language, units: dto.units.map(mapUnit) };
}

function mapUnit(dto: UnitDto): Unit {
  return { id: dto.id, title: dto.title, position: Number(dto.position), lessons: dto.lessons.map(mapLesson) };
}

function mapLesson(dto: LessonDto): Lesson {
  return { id: dto.id, title: dto.title, position: Number(dto.position), isLocked: !dto.isPublished };
}
