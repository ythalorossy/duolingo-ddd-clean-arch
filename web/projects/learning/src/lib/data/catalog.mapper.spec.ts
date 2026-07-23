import { CatalogDto } from '@duolingo/contracts';
import { mapCatalog } from './catalog.mapper';

describe('mapCatalog', () => {
  const dto: CatalogDto = {
    courses: [
      {
        id: 'c1', title: 'Spanish', language: 'es',
        units: [
          {
            id: 'u1', title: 'Basics', position: 1,
            lessons: [
              { id: 'l1', title: 'Greetings', position: 1, isPublished: true },
              { id: 'l2', title: 'Draft', position: 2, isPublished: false },
            ],
          },
        ],
      },
    ],
  };

  it('maps DTO tree into domain Course/Unit/Lesson', () => {
    const [course] = mapCatalog(dto);
    expect(course.title).toBe('Spanish');
    expect(course.units[0].lessons[0].title).toBe('Greetings');
  });

  it('derives a client-side isLocked flag from isPublished', () => {
    const [course] = mapCatalog(dto);
    expect(course.units[0].lessons[0].isLocked).toBe(false); // published
    expect(course.units[0].lessons[1].isLocked).toBe(true);  // unpublished
  });

  it('returns an empty array for an empty catalog', () => {
    expect(mapCatalog({ courses: [] })).toEqual([]);
  });

  it('coerces a numeric-string position from the wire into a domain number', () => {
    // The wire type allows position as a numeric string (.NET 10 OpenAPI int -> ["integer","string"]).
    const wireWithStringPosition: CatalogDto = {
      courses: [{ id: 'c1', title: 'Spanish', language: 'es', units: [
        { id: 'u1', title: 'Basics', position: '3', lessons: [
          { id: 'l1', title: 'Greetings', position: '7', isPublished: true },
        ] },
      ] }],
    };
    const [course] = mapCatalog(wireWithStringPosition);
    expect(course.units[0].position).toBe(3);
    expect(course.units[0].lessons[0].position).toBe(7);
  });
});
