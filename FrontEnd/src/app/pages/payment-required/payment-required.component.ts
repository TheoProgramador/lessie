import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { AuthService, UserProfile } from 'src/app/services/auth.service';
import { SharedModule } from 'src/app/theme/shared/shared.module';

@Component({
  selector: 'app-payment-required',
  imports: [CommonModule, RouterModule, SharedModule],
  templateUrl: './payment-required.component.html',
  styleUrls: ['./payment-required.component.scss']
})
export class PaymentRequiredComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  profile?: UserProfile;

  ngOnInit(): void {
    this.authService.me().subscribe({
      next: (profile) => {
        this.profile = profile;
        if (profile.isAdmin || profile.hasActiveSubscription) {
          this.router.navigate(['/dashboard']);
        }
      }
    });
  }

  logout(): void {
    this.authService.logout().subscribe({
      complete: () => this.router.navigate(['/login'])
    });
  }
}
