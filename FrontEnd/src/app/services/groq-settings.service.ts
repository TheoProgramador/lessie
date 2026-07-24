import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ProviderKeyStatus {
  provider: string;
  configured: boolean;
  lastUsedAt?: string;
}

@Injectable({
  providedIn: 'root'
})
export class GroqSettingsService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiBaseUrl;
  private configured = false;
  private pollinationsConfigured = false;

  constructor() {
    localStorage.removeItem('groqApiKey');
  }

  loadStatus(): Observable<ProviderKeyStatus[]> {
    return this.http.get<ProviderKeyStatus[]>(`${this.apiBaseUrl}/api/provider-keys`).pipe(
      tap((statuses) => {
        this.configured = this.isConfigured(statuses, 'Groq');
        this.pollinationsConfigured = this.isConfigured(statuses, 'Pollinations');
      })
    );
  }

  saveApiKey(apiKey: string): Observable<ProviderKeyStatus> {
    return this.http.post<ProviderKeyStatus>(`${this.apiBaseUrl}/api/provider-keys/groq`, { apiKey }).pipe(
      tap((status) => {
        this.configured = status.configured;
      })
    );
  }

  clearApiKey(): Observable<ProviderKeyStatus> {
    return this.http.delete<ProviderKeyStatus>(`${this.apiBaseUrl}/api/provider-keys/groq`).pipe(
      tap((status) => {
        this.configured = status.configured;
      })
    );
  }

  savePollinationsToken(apiKey: string): Observable<ProviderKeyStatus> {
    return this.http.post<ProviderKeyStatus>(`${this.apiBaseUrl}/api/provider-keys/pollinations`, { apiKey }).pipe(
      tap((status) => {
        this.pollinationsConfigured = status.configured;
      })
    );
  }

  clearPollinationsToken(): Observable<ProviderKeyStatus> {
    return this.http.delete<ProviderKeyStatus>(`${this.apiBaseUrl}/api/provider-keys/pollinations`).pipe(
      tap((status) => {
        this.pollinationsConfigured = status.configured;
      })
    );
  }

  hasApiKey(): boolean {
    return this.configured;
  }

  hasPollinationsToken(): boolean {
    return this.pollinationsConfigured;
  }

  private isConfigured(statuses: ProviderKeyStatus[], provider: string): boolean {
    return !!statuses.find((status) => status.provider?.trim().toLowerCase() === provider.toLowerCase())?.configured;
  }
}
