import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from 'src/app/services/auth.service';
import {
  InterviewAnalysisResponse,
  InterviewAnalysisService,
  InterviewTranscriptSegment
} from 'src/app/services/interview-analysis.service';
import { SharedModule } from 'src/app/theme/shared/shared.module';

@Component({
  selector: 'app-interview-analysis',
  imports: [CommonModule, FormsModule, SharedModule],
  templateUrl: './interview-analysis.component.html',
  styleUrls: ['./interview-analysis.component.scss']
})
export class InterviewAnalysisComponent implements OnInit {
  private readonly interviewAnalysisService = inject(InterviewAnalysisService);
  private readonly authService = inject(AuthService);
  private readonly cd = inject(ChangeDetectorRef);

  audioFile: File | null = null;
  candidateName = '';
  roleTitle = '';
  companyName = '';
  interviewContext = '';
  jobDescription = '';
  customInstructions = '';
  loading = false;
  error = '';
  response: InterviewAnalysisResponse | null = null;
  isAdmin = false;
  interviewAnalysisCount = 0;
  interviewAnalysisLimit = 5;

  get canAnalyze(): boolean {
    return Boolean(this.audioFile) && !this.loading && !this.interviewQuotaExceeded;
  }

  get interviewQuotaExceeded(): boolean {
    return !this.isAdmin && this.interviewAnalysisCount >= this.interviewAnalysisLimit;
  }

  get durationLabel(): string {
    return this.formatDuration(this.response?.durationSeconds ?? 0);
  }

  ngOnInit(): void {
    this.loadUsage();
  }

  onAudioSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.audioFile = input.files?.[0] ?? null;
    this.error = '';
  }

  analyze(): void {
    if (!this.audioFile || this.loading) {
      return;
    }

    this.loading = true;
    this.error = '';
    this.response = null;

    this.interviewAnalysisService
      .analyze(this.audioFile, {
        candidateName: this.candidateName.trim(),
        roleTitle: this.roleTitle.trim(),
        companyName: this.companyName.trim(),
        interviewContext: this.interviewContext.trim(),
        jobDescription: this.jobDescription.trim(),
        customInstructions: this.customInstructions.trim()
      })
      .subscribe({
        next: (response) => {
          this.loading = false;
          this.response = response;
          this.loadUsage();
          this.cd.detectChanges();
        },
        error: (error: HttpErrorResponse) => {
          this.loading = false;
          this.error = this.getErrorMessage(error);
          this.cd.detectChanges();
        }
      });
  }

  trackSegment(_: number, segment: InterviewTranscriptSegment): string {
    return `${segment.start}-${segment.end}-${segment.text}`;
  }

  downloadTranscript(): void {
    if (!this.response) {
      return;
    }

    const content = this.response.segments
      .map((segment) => `[${segment.startTime} - ${segment.endTime}] ${segment.text}`)
      .join('\n');
    this.download(content, 'transcricao-entrevista.txt');
  }

  downloadAnalysis(): void {
    if (!this.response) {
      return;
    }

    const content = [
      this.response.warning,
      '',
      `Modelo transcricao: ${this.response.transcriptionModel}`,
      `Modelo analise: ${this.response.analysisModel}`,
      `Duracao: ${this.durationLabel}`,
      `Custo Groq estimado: US$ ${this.response.estimatedGroqCostUsd.toFixed(6)} / R$ ${this.response.estimatedGroqCostBrl.toFixed(4)}`,
      '',
      this.response.analysis
    ].join('\n');
    this.download(content, 'analise-entrevista.txt');
  }

  private formatDuration(seconds: number): string {
    const safeSeconds = Math.max(0, Math.round(seconds));
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);
    const remainingSeconds = safeSeconds % 60;

    if (hours > 0) {
      return `${hours}h ${minutes}min ${remainingSeconds}s`;
    }

    return `${minutes}min ${remainingSeconds}s`;
  }

  private getErrorMessage(error: HttpErrorResponse): string {
    if (error.error?.message) {
      return error.error.message;
    }

    if (typeof error.error === 'string') {
      try {
        const parsed = JSON.parse(error.error);
        return parsed?.message ?? error.error;
      } catch {
        return error.error;
      }
    }

    return 'Nao foi possivel analisar a entrevista.';
  }

  private loadUsage(): void {
    this.authService.me().subscribe({
      next: (profile) => {
        this.isAdmin = profile.isAdmin;
        this.interviewAnalysisCount = profile.interviewAnalysisCount;
        this.interviewAnalysisLimit = profile.interviewAnalysisLimit;
        this.cd.detectChanges();
      },
      error: () => {
        this.cd.detectChanges();
      }
    });
  }

  private download(content: string, filename: string): void {
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  }
}
