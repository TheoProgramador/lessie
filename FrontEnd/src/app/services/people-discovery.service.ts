import { HttpClient } from '@angular/common/http';
import { inject, Injectable, NgZone } from '@angular/core';
import { Observable, Observer } from 'rxjs';
import { environment } from '../../environments/environment';
import { TokenService } from './token.service';

export interface PeopleDiscoveryPerson {
  resultKey: string;
  name: string;
  title: string;
  company: string;
  location: string;
  contactInfo: string;
  profileUrl: string;
  source: string;
  resumeSent: boolean;
}

export interface PeopleDiscoverySearchResponse {
  success: boolean;
  source: string;
  toolName: string;
  summary: string;
  results: PeopleDiscoveryPerson[];
  error: string | null;
}

export interface PeopleDiscoveryJobSearchRequest {
  keywords: string;
  location?: string;
  maxPages: number;
  datePosted?: string;
  jobType?: string;
  experienceLevel?: string;
  workType?: string;
  easyApply: boolean;
  sortBy?: string;
}

export interface PeopleDiscoveryJob {
  resultKey: string;
  title: string;
  company: string;
  location: string;
  jobId: string;
  jobUrl: string;
  insight: string;
  metadata: string;
  source: string;
  resumeSent: boolean;
}

export interface PeopleDiscoveryJobSearchResponse {
  success: boolean;
  source: string;
  toolName: string;
  summary: string;
  results: PeopleDiscoveryJob[];
  error: string | null;
}

export interface PeopleDiscoveryProgressEvent {
  level: string;
  message: string;
  details?: string | null;
  progress?: number | null;
  total?: number | null;
  elapsedSeconds?: number | null;
  peopleCount?: number | null;
  processId?: number | null;
  processRunning?: boolean | null;
}

export type PeopleDiscoveryStreamEvent =
  | { type: 'progress'; data: PeopleDiscoveryProgressEvent }
  | { type: 'result'; data: PeopleDiscoverySearchResponse }
  | { type: 'error'; data: { message: string } };

export type PeopleDiscoveryJobStreamEvent =
  | { type: 'progress'; data: PeopleDiscoveryProgressEvent }
  | { type: 'result'; data: PeopleDiscoveryJobSearchResponse }
  | { type: 'error'; data: { message: string } };

@Injectable({
  providedIn: 'root'
})
export class PeopleDiscoveryService {
  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);
  private readonly tokenService = inject(TokenService);
  private readonly apiBaseUrl = environment.apiBaseUrl;

  search(query: string): Observable<PeopleDiscoverySearchResponse> {
    return this.http.post<PeopleDiscoverySearchResponse>(`${this.apiBaseUrl}/api/people-discovery/search`, { query });
  }

  searchPosts(query: string, location?: string): Observable<PeopleDiscoverySearchResponse> {
    return this.http.post<PeopleDiscoverySearchResponse>(`${this.apiBaseUrl}/api/people-discovery/posts/search`, { query, location });
  }

  searchJobs(request: PeopleDiscoveryJobSearchRequest): Observable<PeopleDiscoveryJobSearchResponse> {
    return this.http.post<PeopleDiscoveryJobSearchResponse>(`${this.apiBaseUrl}/api/people-discovery/jobs/search`, request);
  }

  markResumeSent(resultKey: string): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.apiBaseUrl}/api/people-discovery/results/resume-sent`, { resultKey });
  }

  searchWithProgress(query: string): Observable<PeopleDiscoveryStreamEvent> {
    return new Observable<PeopleDiscoveryStreamEvent>((observer) => {
      const controller = new AbortController();
      const token = this.tokenService.getAccessToken();

      if (!token) {
        this.emitError(observer, new Error('Missing access token.'));
        return () => controller.abort();
      }

      this.readStream(`${this.apiBaseUrl}/api/people-discovery/search/stream`, { query }, token, controller, observer);
      return () => controller.abort();
    });
  }

  searchPostsWithProgress(query: string, location?: string): Observable<PeopleDiscoveryStreamEvent> {
    return new Observable<PeopleDiscoveryStreamEvent>((observer) => {
      const controller = new AbortController();
      const token = this.tokenService.getAccessToken();

      if (!token) {
        this.emitError(observer, new Error('Missing access token.'));
        return () => controller.abort();
      }

      this.readStream(`${this.apiBaseUrl}/api/people-discovery/posts/search/stream`, { query, location }, token, controller, observer);
      return () => controller.abort();
    });
  }

  searchJobsWithProgress(request: PeopleDiscoveryJobSearchRequest): Observable<PeopleDiscoveryJobStreamEvent> {
    return new Observable<PeopleDiscoveryJobStreamEvent>((observer) => {
      const controller = new AbortController();
      const token = this.tokenService.getAccessToken();

      if (!token) {
        this.emitError(observer, new Error('Missing access token.'));
        return () => controller.abort();
      }

      this.readStream(`${this.apiBaseUrl}/api/people-discovery/jobs/search/stream`, request, token, controller, observer);
      return () => controller.abort();
    });
  }

  private async readStream(
    url: string,
    body: object,
    token: string,
    controller: AbortController,
    observer: Observer<PeopleDiscoveryStreamEvent | PeopleDiscoveryJobStreamEvent>
  ): Promise<void> {
    try {
      const response = await fetch(url, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${token}`,
          'Content-Type': 'application/json',
          Accept: 'application/x-ndjson'
        },
        body: JSON.stringify(body),
        signal: controller.signal
      });

      if (!response.ok || !response.body) {
        this.emitError(observer, new Error('Unable to run People Discovery.'));
        return;
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';

      while (true) {
        const { value, done } = await reader.read();
        if (done) {
          break;
        }

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() ?? '';

        for (const line of lines) {
          const trimmed = line.trim();
          if (!trimmed) {
            continue;
          }

          this.emitNext(observer, JSON.parse(trimmed) as PeopleDiscoveryStreamEvent | PeopleDiscoveryJobStreamEvent);
        }
      }

      if (buffer.trim()) {
        this.emitNext(observer, JSON.parse(buffer.trim()) as PeopleDiscoveryStreamEvent | PeopleDiscoveryJobStreamEvent);
      }

      this.emitComplete(observer);
    } catch (error) {
      if (!controller.signal.aborted) {
        this.emitError(observer, error);
      }
    }
  }

  private emitNext(
    observer: Observer<PeopleDiscoveryStreamEvent | PeopleDiscoveryJobStreamEvent>,
    event: PeopleDiscoveryStreamEvent | PeopleDiscoveryJobStreamEvent
  ): void {
    this.zone.run(() => observer.next(event));
  }

  private emitError(observer: Observer<PeopleDiscoveryStreamEvent | PeopleDiscoveryJobStreamEvent>, error: unknown): void {
    this.zone.run(() => observer.error(error));
  }

  private emitComplete(observer: Observer<PeopleDiscoveryStreamEvent | PeopleDiscoveryJobStreamEvent>): void {
    this.zone.run(() => observer.complete());
  }
}
