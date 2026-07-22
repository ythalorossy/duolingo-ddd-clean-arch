import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadChildren: () => import('@duolingo/learning').then((m) => m.LEARNING_ROUTES),
  },
];
