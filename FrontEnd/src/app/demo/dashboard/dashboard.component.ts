import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthService, UserProfile } from 'src/app/services/auth.service';
import { SharedModule } from 'src/app/theme/shared/shared.module';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, RouterModule, SharedModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly changeDetectorRef = inject(ChangeDetectorRef);

  profile?: UserProfile;
  loading = true;
  error = '';

  ngOnInit(): void {
    this.authService.me().subscribe({
      next: (profile) => {
        this.profile = profile;
        this.loading = false;
        this.changeDetectorRef.detectChanges();
      },
      error: () => {
        this.error = 'Nao foi possivel carregar o usuario.';
        this.loading = false;
        this.changeDetectorRef.detectChanges();
      }
    });
  }

  logout(): void {
    this.authService.logout().subscribe({
      complete: () => this.router.navigate(['/login'])
    });
  }

  get remainingResumeAnalyses(): string {
    return this.formatRemaining(this.profile?.resumeAnalysisLimit, this.profile?.resumeAnalysisCount);
  }

  get remainingChatConversations(): string {
    return this.formatRemaining(this.profile?.chatConversationLimit, this.profile?.chatConversationCount);
  }

  get remainingInterviewAnalyses(): string {
    return this.formatRemaining(this.profile?.interviewAnalysisLimit, this.profile?.interviewAnalysisCount);
  }

  private formatRemaining(limit?: number, used?: number): string {
    if (limit === undefined || used === undefined) {
      return '-';
    }

    return String(Math.max(limit - used, 0));
  }
}
