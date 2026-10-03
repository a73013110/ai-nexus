import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/identity/login-page').then((module) => module.LoginPage),
  },
  {
    path: 'chat',
    loadComponent: () =>
      import('./features/chat/chat-workspace').then((module) => module.ChatWorkspace),
  },
  {
    path: 'chat/:id',
    loadComponent: () =>
      import('./features/chat/chat-workspace').then((module) => module.ChatWorkspace),
  },
  { path: '', pathMatch: 'full', redirectTo: 'chat' },
  { path: '**', redirectTo: 'chat' },
];
