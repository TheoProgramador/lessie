import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface InterviewAnalysisContext {
  candidateName: string;
  roleTitle: string;
  companyName: string;
  interviewContext: string;
  jobDescription: string;
  customInstructions: string;
}

export interface InterviewTranscriptSegment {
  start: number;
  end: number;
  startTime: string;
  endTime: string;
  text: string;
  averageLogProbability?: number | null;
  noSpeechProbability?: number | null;
  compressionRatio?: number | null;
}

export interface InterviewAnalysisResponse {
  warning: string;
  transcriptionModel: string;
  analysisModel: string;
  estimatedGroqCostUsd: number;
  estimatedGroqCostBrl: number;
  durationSeconds: number;
  transcriptText: string;
  segments: InterviewTranscriptSegment[];
  analysis: string;
}

@Injectable({
  providedIn: 'root'
})
export class InterviewAnalysisService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiBaseUrl;

  analyze(audio: File, context: InterviewAnalysisContext): Observable<InterviewAnalysisResponse> {
    const formData = new FormData();
    formData.append('audio', audio);
    formData.append('candidateName', context.candidateName);
    formData.append('roleTitle', context.roleTitle);
    formData.append('companyName', context.companyName);
    formData.append('interviewContext', context.interviewContext);
    formData.append('jobDescription', context.jobDescription);
    formData.append('customInstructions', context.customInstructions);

    return this.http.post<InterviewAnalysisResponse>(`${this.apiBaseUrl}/api/interview-analysis/analyze`, formData);
  }
}
