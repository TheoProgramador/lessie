import { AfterViewInit, ChangeDetectorRef, Component, NgZone, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { switchMap } from 'rxjs';
import { AuthService } from 'src/app/services/auth.service';
import { SharedModule } from 'src/app/theme/shared/shared.module';
import { environment } from 'src/environments/environment';

interface GoogleIdentity {
  accounts: {
    id: {
      initialize: (options: { client_id: string; callback: (response: { credential?: string }) => void }) => void;
      renderButton: (parent: HTMLElement, options: { theme: string; size: string; text: string; width: number }) => void;
    };
  };
}

@Component({
  selector: 'app-auth-signin',
  imports: [CommonModule, RouterModule, SharedModule],
  templateUrl: './auth-signin.component.html',
  styleUrls: ['./auth-signin.component.scss']
})
export class AuthSigninComponent implements AfterViewInit {
  private cd = inject(ChangeDetectorRef);
  private zone = inject(NgZone);
  private authService = inject(AuthService);
  private router = inject(Router);

  error = signal('');
  loading = signal(false);
  readonly showDevelopmentLogin = !environment.production && this.isLocalhost() && this.isLocalApi();

  ngAfterViewInit(): void {
    this.initializeGoogleSignIn();
  }

  private initializeGoogleSignIn(retries = 10): void {
    const google = (window as Window & { google?: GoogleIdentity }).google;

    if (!google) {
      if (retries > 0) {
        setTimeout(() => this.initializeGoogleSignIn(retries - 1), 250);
        return;
      }

      this.error.set('Google Identity Services nao carregou.');
      this.cd.detectChanges();
      return;
    }

    if (!environment.googleClientId) {
      this.error.set('Configure googleClientId em environment.ts.');
      this.cd.detectChanges();
      return;
    }

    const buttonContainer = document.getElementById('google-signin-btn');
    if (!buttonContainer) {
      return;
    }

    google.accounts.id.initialize({
      client_id: environment.googleClientId,
      callback: this.handleCredentialResponse.bind(this)
    });

    google.accounts.id.renderButton(buttonContainer, {
      theme: 'outline',
      size: 'large',
      text: 'signin_with',
      width: 260
    });
  }

  private handleCredentialResponse(response: { credential?: string }): void {
    this.zone.run(() => this.signInWithCredential(response.credential));
  }

  private signInWithCredential(credential?: string): void {
    this.error.set('');
    this.loading.set(true);

    if (!credential) {
      this.loading.set(false);
      this.error.set('Credential Google ausente.');
      this.cd.detectChanges();
      return;
    }

    this.authService.loginWithGoogleCredential(credential).pipe(
      switchMap(() => this.authService.me())
    ).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        this.loading.set(false);
        this.error.set(error?.status === 401 ? 'Sessao criada, mas o token foi recusado pela API.' : 'Login Google falhou.');
        this.cd.detectChanges();
      }
    });
  }

  signInAsDevelopmentAdmin(): void {
    this.error.set('');
    this.loading.set(true);

    this.authService.loginAsDevelopmentAdmin().pipe(
      switchMap(() => this.authService.me())
    ).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Login de desenvolvimento falhou.');
        this.cd.detectChanges();
      }
    });
  }

  private isLocalhost(): boolean {
    return window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
  }

  private isLocalApi(): boolean {
    try {
      const apiUrl = new URL(environment.apiBaseUrl);
      return apiUrl.hostname === 'localhost' || apiUrl.hostname === '127.0.0.1';
    } catch {
      return false;
    }
  }
}
