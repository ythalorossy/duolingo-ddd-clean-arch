import { routes } from './app.routes';

describe('app routes', () => {
  it('lazy-loads the learning catalog at the default path', () => {
    const learn = routes.find((r) => r.path === '');
    expect(learn).toBeTruthy();
    expect(typeof learn!.loadChildren).toBe('function');
  });
});
