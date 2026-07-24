import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface JobOpportunity {
  resultKey: string;
  id: string;
  title: string;
  company: string;
  location: string;
  country: string;
  remoteType: string;
  employmentType: string;
  salary: string;
  publishedAt: string | null;
  date: string;
  description: string;
  requirements: string;
  url: string;
  applyUrl: string;
  contactEmail: string;
  contactSubject: string;
  source: string;
  provider: string;
}

export interface OpportunitySearchRequest {
  query: string;
  location?: string;
  limit: number;
}

export interface OpportunitySearchResponse {
  success: boolean;
  source: string;
  toolName: string;
  summary: string;
  results: JobOpportunity[];
  error: string | null;
}

export interface OpportunityDetailsResponse {
  success: boolean;
  source: string;
  toolName: string;
  result: JobOpportunity | null;
  error: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class OpportunityDiscoveryService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiBaseUrl;

  search(request: OpportunitySearchRequest): Observable<OpportunitySearchResponse> {
    return this.http.post<OpportunitySearchResponse>(`${this.apiBaseUrl}/api/opportunity-discovery/search`, request);
  }

  details(jobId: string, revealContact: boolean): Observable<OpportunityDetailsResponse> {
    return this.http.post<OpportunityDetailsResponse>(`${this.apiBaseUrl}/api/opportunity-discovery/details`, {
      jobId,
      revealContact
    });
  }
}
