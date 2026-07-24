import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';

export const paymentGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.me().pipe(
    map((profile) => (profile.isAdmin || profile.hasActiveSubscription ? true : router.createUrlTree(['/payment-required']))),
    catchError(() => of(router.createUrlTree(['/login'])))
  );
};
