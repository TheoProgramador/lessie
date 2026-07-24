// angular import
import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';

// bootstrap import
import { NgbDropdownConfig } from '@ng-bootstrap/ng-bootstrap';

// project import
import { AuthService, UserProfile } from 'src/app/services/auth.service';
import { SharedModule } from 'src/app/theme/shared/shared.module';

@Component({
  selector: 'app-nav-right',
  imports: [RouterModule, SharedModule],
  templateUrl: './nav-right.component.html',
  styleUrls: ['./nav-right.component.scss'],
  providers: [NgbDropdownConfig]
})
export class NavRightComponent implements OnInit {
  // public props
  profile?: UserProfile;
  loadingProfile = true;

  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly changeDetectorRef = inject(ChangeDetectorRef);

  // constructor
  constructor() {
    const config = inject(NgbDropdownConfig);
    config.placement = 'bottom-right';
  }

  ngOnInit(): void {
    this.authService.me().subscribe({
      next: (profile) => {
        this.profile = profile;
        this.loadingProfile = false;
        this.changeDetectorRef.detectChanges();
      },
      error: () => {
        this.loadingProfile = false;
        this.changeDetectorRef.detectChanges();
      }
    });
  }

  get displayName(): string {
    return this.profile?.name || 'Usuario';
  }

  get displayEmail(): string {
    return this.profile?.email || '';
  }

  get profilePhotoUrl(): string {
    return this.profile?.pictureUrl || '';
  }

  logout(): void {
    this.authService.logout().subscribe({
      complete: () => this.router.navigate(['/login'])
    });
  }
}
