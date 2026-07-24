import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { TokenService } from './token.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const tokenService = inject(TokenService);
  const router = inject(Router);
  const accessToken = tokenService.getAccessToken();
  const isAuthEndpoint = request.url.includes('/api/auth/');

  const authenticatedRequest =
    accessToken && !isAuthEndpoint ? request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } }) : request;

  return next(authenticatedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 402) {
        router.navigate(['/payment-required']);
        return throwError(() => error);
      }

      if (error.status !== 401 || isAuthEndpoint || request.headers.has('x-refresh-attempt')) {
        return throwError(() => error);
      }

      return authService.refresh().pipe(
        switchMap((response) => {
          const retryRequest = request.clone({
            setHeaders: {
              Authorization: `Bearer ${response.accessToken}`,
              'x-refresh-attempt': 'true'
            }
          });

          return next(retryRequest);
        }),
        catchError((refreshError) => {
          authService.clearSession();
          router.navigate(['/login']);
          return throwError(() => refreshError);
        })
      );
    })
  );
};
