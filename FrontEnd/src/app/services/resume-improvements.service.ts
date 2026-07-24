import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ChatMessage } from './chatbot.service';

export interface ResumeAtsSectionScore {
  name: string;
  score: number;
  status: string;
  summary: string;
  recommendations: string[];
}

export interface ResumeAtsAnalysis {
  provider: string;
  overallScore: number;
  analyzedAt: string;
  sections: ResumeAtsSectionScore[];
  strengths: string[];
  risks: string[];
  recommendations: string[];
  keywordsPresent: string[];
  keywordsMissing: string[];
  keywordsPartial: string[];
  jobSearchKeywords: string[];
  criticalGaps: string[];
  matchRecommendation: string;
  subscores: Record<string, number>;
  requirementCoverage: ResumeAtsRequirementCoverage[];
  keywordStrategy: ResumeAtsKeywordStrategy[];
  canonicalResumeJson: string;
}

export interface ResumeAtsRequirementCoverage {
  requirement: string;
  status: string;
  evidence: string;
  recommendation: string;
}

export interface ResumeAtsKeywordStrategy {
  group: string;
  targetSection: string;
  keywords: string[];
  instruction: string;
}

export interface ResumeImprovementAnalyzeResponse {
  sessionId: string;
  message: string;
  resumeText: string;
  jobContext: string;
  optimizedResume: string;
  readyToExport: boolean;
  atsAnalysis?: ResumeAtsAnalysis | null;
}

export interface ResumeImprovementAdditionalContext {
  linkedinProfile?: File | null;
  linkedinProfileUrl: string;
  githubProfileUrl: string;
  portfolioUrl: string;
  personalInfo: string;
  customInstructions: string;
  jobDescription: string;
}

export interface ResumeImprovementChatRequest {
  sessionId?: string;
  forkFromSession?: boolean;
  resumeText: string;
  jobContext: string;
  optimizedResume: string;
  message: string;
  linkedinProfileUrl: string;
  githubProfileUrl: string;
  portfolioUrl: string;
  history: ChatMessage[];
}

export interface ResumeImprovementChatResponse {
  sessionId: string;
  message: string;
  sentPayloadPreview: string;
  optimizedResume: string;
  readyToExport: boolean;
  atsAnalysis?: ResumeAtsAnalysis | null;
}

export interface ResumeImprovementHistoryItem {
  id: string;
  title: string;
  resumeFileName: string;
  readyToExport: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ResumeImprovementSessionDetail {
  id: string;
  title: string;
  resumeFileName: string;
  jobContext: string;
  optimizedResume: string;
  readyToExport: boolean;
  hasResumeContext: boolean;
  linkedinProfileUrl: string;
  githubProfileUrl: string;
  portfolioUrl: string;
  atsAnalysis?: ResumeAtsAnalysis | null;
  messages: ChatMessage[];
}

export interface ResumeImprovementSaveResponse {
  sessionId: string;
  optimizedResume: string;
  readyToExport: boolean;
  updatedAt: string;
  atsAnalysis?: ResumeAtsAnalysis | null;
}

export interface ResumeImprovementRenameResponse {
  sessionId: string;
  title: string;
  updatedAt: string;
}

export interface ResumeImprovementProfileLinksResponse {
  sessionId: string;
  linkedinProfileUrl: string;
  githubProfileUrl: string;
  portfolioUrl: string;
  updatedAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class ResumeImprovementsService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiBaseUrl;

  analyze(
    resume: File,
    jobScreenshots: File[],
    additionalContext: ResumeImprovementAdditionalContext
  ): Observable<ResumeImprovementAnalyzeResponse> {
    const formData = new FormData();
    formData.append('resume', resume);
    for (const screenshot of jobScreenshots) {
      formData.append('jobScreenshots', screenshot);
    }
    if (additionalContext.linkedinProfile) {
      formData.append('linkedinProfile', additionalContext.linkedinProfile);
    }
    formData.append('linkedinProfileUrl', additionalContext.linkedinProfileUrl);
    formData.append('githubProfileUrl', additionalContext.githubProfileUrl);
    formData.append('portfolioUrl', additionalContext.portfolioUrl);
    formData.append('personalInfo', additionalContext.personalInfo);
    formData.append('customInstructions', additionalContext.customInstructions);
    formData.append('jobDescription', additionalContext.jobDescription);

    return this.http.post<ResumeImprovementAnalyzeResponse>(`${this.apiBaseUrl}/api/resume-improvements/analyze`, formData);
  }

  chat(request: ResumeImprovementChatRequest): Observable<ResumeImprovementChatResponse> {
    return this.http.post<ResumeImprovementChatResponse>(`${this.apiBaseUrl}/api/resume-improvements/chat`, request);
  }

  optimizeForJob(
    sessionId: string,
    jobScreenshots: File[],
    forkFromSession: boolean,
    linkedinProfileUrl: string,
    githubProfileUrl: string,
    portfolioUrl: string
  ): Observable<ResumeImprovementChatResponse> {
    const formData = new FormData();
    for (const screenshot of jobScreenshots) {
      formData.append('jobScreenshots', screenshot);
    }
    formData.append('forkFromSession', String(forkFromSession));
    formData.append('linkedinProfileUrl', linkedinProfileUrl);
    formData.append('githubProfileUrl', githubProfileUrl);
    formData.append('portfolioUrl', portfolioUrl);

    return this.http.post<ResumeImprovementChatResponse>(`${this.apiBaseUrl}/api/resume-improvements/history/${sessionId}/job-screenshots`, formData);
  }

  history(): Observable<ResumeImprovementHistoryItem[]> {
    return this.http.get<ResumeImprovementHistoryItem[]>(`${this.apiBaseUrl}/api/resume-improvements/history`);
  }

  getSession(sessionId: string): Observable<ResumeImprovementSessionDetail> {
    return this.http.get<ResumeImprovementSessionDetail>(`${this.apiBaseUrl}/api/resume-improvements/history/${sessionId}`);
  }

  deleteSession(sessionId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiBaseUrl}/api/resume-improvements/history/${sessionId}`);
  }

  saveOptimizedResume(sessionId: string, optimizedResume: string, forkFromSession = false): Observable<ResumeImprovementSaveResponse> {
    return this.http.put<ResumeImprovementSaveResponse>(`${this.apiBaseUrl}/api/resume-improvements/history/${sessionId}/optimized-resume`, {
      optimizedResume,
      forkFromSession
    });
  }

  renameSession(sessionId: string, title: string): Observable<ResumeImprovementRenameResponse> {
    return this.http.put<ResumeImprovementRenameResponse>(`${this.apiBaseUrl}/api/resume-improvements/history/${sessionId}/title`, {
      title
    });
  }

  updateProfileLinks(
    sessionId: string,
    linkedinProfileUrl: string,
    githubProfileUrl: string,
    portfolioUrl: string
  ): Observable<ResumeImprovementProfileLinksResponse> {
    return this.http.put<ResumeImprovementProfileLinksResponse>(`${this.apiBaseUrl}/api/resume-improvements/history/${sessionId}/profile-links`, {
      linkedinProfileUrl,
      githubProfileUrl,
      portfolioUrl
    });
  }

  export(content: string, format: 'docx' | 'pdf'): Observable<Blob> {
    return this.http.post(`${this.apiBaseUrl}/api/resume-improvements/export`, { content, format }, { responseType: 'blob' });
  }
}
