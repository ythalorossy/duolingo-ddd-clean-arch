// Frontend domain model for the Learning context. Deliberately thin — the authoritative
// invariants live server-side. Holds only view-model shape + genuinely client-side display state.
export interface Lesson {
  readonly id: string;
  readonly title: string;
  readonly position: number;
  /** Client-side display state: an unpublished lesson renders as locked. */
  readonly isLocked: boolean;
}

export interface Unit {
  readonly id: string;
  readonly title: string;
  readonly position: number;
  readonly lessons: readonly Lesson[];
}

export interface Course {
  readonly id: string;
  readonly title: string;
  readonly language: string | null;
  readonly units: readonly Unit[];
}
