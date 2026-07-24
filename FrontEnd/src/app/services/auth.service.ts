import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { finalize, map, tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { TokenService } from './token.service';

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

interface AuthApiResponse {
  accessToken?: string;
  refreshToken?: string;
  expiresIn?: number;
  AccessToken?: string;
  RefreshToken?: string;
  ExpiresIn?: number;
}

export interface UserProfile {
  id: string;
  name: string;
  email: string;
  pictureUrl?: string;
  isAdmin: boolean;
  hasActiveSubscription: boolean;
  isPaid: boolean;
  paidUntil?: string | null;
  resumeAnalysisCount: number;
  resumeAnalysisLimit: number;
  chatConversationCount: number;
  chatConversationLimit: number;
  interviewAnalysisCount: number;
  interviewAnalysisLimit: number;
  creditBalance: number;
  totalCreditsPurchased: number;
}

interface UserProfileApiResponse {
  id?: string;
  name?: string;
  email?: string;
  pictureUrl?: string;
  Id?: string;
  Name?: string;
  Email?: string;
  PictureUrl?: string;
  isAdmin?: boolean;
  hasActiveSubscription?: boolean;
  isPaid?: boolean;
  paidUntil?: string | null;
  resumeAnalysisCount?: number;
  resumeAnalysisLimit?: number;
  chatConversationCount?: number;
  chatConversationLimit?: number;
  interviewAnalysisCount?: number;
  interviewAnalysisLimit?: number;
  IsAdmin?: boolean;
  HasActiveSubscription?: boolean;
  IsPaid?: boolean;
  PaidUntil?: string | null;
  ResumeAnalysisCount?: number;
  ResumeAnalysisLimit?: number;
  ChatConversationCount?: number;
  ChatConversationLimit?: number;
  InterviewAnalysisCount?: number;
  InterviewAnalysisLimit?: number;
  creditBalance?: number;
  totalCreditsPurchased?: number;
  CreditBalance?: number;
  TotalCreditsPurchased?: number;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiBaseUrl = environment.apiBaseUrl;
  private readonly http = inject(HttpClient);
  private readonly tokenService = inject(TokenService);

  loginWithGoogleCredential(credential: string): Observable<AuthResponse> {
    return this.http.post<AuthApiResponse>(`${this.apiBaseUrl}/api/auth/google`, { credential }).pipe(
      map((response) => this.normalizeAuthResponse(response)),
      tap((response) => this.tokenService.setTokens(response.accessToken, response.refreshToken))
    );
  }

  loginAsDevelopmentAdmin(): Observable<AuthResponse> {
    return this.http.post<AuthApiResponse>(`${this.apiBaseUrl}/api/auth/dev-admin`, {}).pipe(
      map((response) => this.normalizeAuthResponse(response)),
      tap((response) => this.tokenService.setTokens(response.accessToken, response.refreshToken))
    );
  }

  refresh(): Observable<AuthResponse> {
    const refreshToken = this.tokenService.getRefreshToken();
    if (!refreshToken) {
      return throwError(() => new Error('Refresh token is missing.'));
    }

    return this.http.post<AuthApiResponse>(`${this.apiBaseUrl}/api/auth/refresh`, { refreshToken }).pipe(
      map((response) => this.normalizeAuthResponse(response)),
      tap((response) => this.tokenService.setTokens(response.accessToken, response.refreshToken))
    );
  }

  logout(): Observable<void> {
    const refreshToken = this.tokenService.getRefreshToken();
    if (!refreshToken) {
      this.clearSession();
      return of(undefined);
    }

    return this.http.post<void>(`${this.apiBaseUrl}/api/auth/logout`, { refreshToken }).pipe(finalize(() => this.clearSession()));
  }

  me(): Observable<UserProfile> {
    return this.http.get<UserProfileApiResponse>(`${this.apiBaseUrl}/api/me`).pipe(
      map((response) => {
        const tokenProfile = this.readProfileFromAccessToken();
        return {
          id: response.id ?? response.Id ?? tokenProfile.id ?? '',
          name: response.name ?? response.Name ?? tokenProfile.name ?? '',
          email: response.email ?? response.Email ?? tokenProfile.email ?? '',
          pictureUrl: response.pictureUrl ?? response.PictureUrl ?? tokenProfile.pictureUrl,
          isAdmin: response.isAdmin ?? response.IsAdmin ?? tokenProfile.isAdmin ?? false,
          hasActiveSubscription: response.hasActiveSubscription ?? response.HasActiveSubscription ?? tokenProfile.hasActiveSubscription ?? false,
          isPaid: response.isPaid ?? response.IsPaid ?? false,
          paidUntil: response.paidUntil ?? response.PaidUntil ?? null,
          resumeAnalysisCount: response.resumeAnalysisCount ?? response.ResumeAnalysisCount ?? 0,
          resumeAnalysisLimit: response.resumeAnalysisLimit ?? response.ResumeAnalysisLimit ?? 20,
          chatConversationCount: response.chatConversationCount ?? response.ChatConversationCount ?? 0,
          chatConversationLimit: response.chatConversationLimit ?? response.ChatConversationLimit ?? 50,
          interviewAnalysisCount: response.interviewAnalysisCount ?? response.InterviewAnalysisCount ?? 0,
          interviewAnalysisLimit: response.interviewAnalysisLimit ?? response.InterviewAnalysisLimit ?? 5,
          creditBalance: response.creditBalance ?? response.CreditBalance ?? 0,
          totalCreditsPurchased: response.totalCreditsPurchased ?? response.TotalCreditsPurchased ?? 0
        };
      })
    );
  }

  clearSession(): void {
    this.tokenService.clear();
  }

  private normalizeAuthResponse(response: AuthApiResponse): AuthResponse {
    const accessToken = response.accessToken ?? response.AccessToken;
    const refreshToken = response.refreshToken ?? response.RefreshToken;
    const expiresIn = response.expiresIn ?? response.ExpiresIn ?? 900;

    if (!accessToken || !refreshToken) {
      throw new Error('Auth response did not include access and refresh tokens.');
    }

    return { accessToken, refreshToken, expiresIn };
  }

  private readProfileFromAccessToken(): Partial<UserProfile> {
    const accessToken = this.tokenService.getAccessToken();
    const payload = accessToken?.split('.')[1];
    if (!payload) {
      return {};
    }

    try {
      const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
      const padded = normalized.padEnd(normalized.length + ((4 - (normalized.length % 4)) % 4), '=');
      const json = JSON.parse(atob(padded)) as Record<string, string>;
      return {
        id: json['sub'] ?? json['nameid'],
        name: json['name'],
        email: json['email'],
        pictureUrl: json['picture'],
        isAdmin: json['is_admin'] === 'true',
        hasActiveSubscription: json['is_admin'] === 'true'
      };
    } catch {
      return {};
    }
  }
}
