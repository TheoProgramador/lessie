// angular import
import { Component, HostBinding, Input, output } from '@angular/core';

// project import
import { SharedModule } from 'src/app/theme/shared/shared.module';
import { NavLogoComponent } from './nav-logo/nav-logo.component';
import { NavContentComponent } from './nav-content/nav-content.component';

@Component({
  selector: 'app-navigation',
  imports: [SharedModule, NavLogoComponent, NavContentComponent],
  templateUrl: './navigation.component.html',
  styleUrls: ['./navigation.component.scss']
})
export class NavigationComponent {
  // public props
  NavCollapse = output();
  NavCollapsedMob = output();
  @Input() navCollapsed = false;
  @Input() navCollapsedMob = false;
  windowWidth: number;

  @HostBinding('class.navbar-collapsed')
  get isCollapsed(): boolean {
    return this.navCollapsed;
  }

  @HostBinding('class.mob-open')
  get isMobOpen(): boolean {
    return this.navCollapsedMob;
  }

  // constructor
  constructor() {
    this.windowWidth = window.innerWidth;
  }

  // public method
  navCollapse() {
    if (this.windowWidth >= 992) {
      this.NavCollapse.emit();
    }
  }

  navCollapseMob() {
    if (this.windowWidth < 992) {
      this.NavCollapsedMob.emit();
    }
  }
}
