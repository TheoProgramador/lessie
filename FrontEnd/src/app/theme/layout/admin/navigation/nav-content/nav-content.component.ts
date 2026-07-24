// angular import
import { Component, OnInit, inject, output } from '@angular/core';
import { Location } from '@angular/common';

import { NavigationItem, NavigationItems } from '../navigation';
import { SharedModule } from 'src/app/theme/shared/shared.module';
import { NavGroupComponent } from './nav-group/nav-group.component';
import { AuthService } from 'src/app/services/auth.service';

@Component({
  selector: 'app-nav-content',
  imports: [SharedModule, NavGroupComponent],
  templateUrl: './nav-content.component.html',
  styleUrls: ['./nav-content.component.scss']
})
export class NavContentComponent implements OnInit {
  private location = inject(Location);
  private authService = inject(AuthService);

  navigations!: NavigationItem[];
  wrapperWidth: number;
  windowWidth = window.innerWidth;

  NavCollapsedMob = output();

  // constructor
  constructor() {
    this.navigations = this.filterNavigation(NavigationItems, false);
  }

  ngOnInit() {
    this.authService.me().subscribe({
      next: (profile) => {
        this.navigations = this.filterNavigation(NavigationItems, profile.isAdmin);
      },
      error: () => {
        this.navigations = this.filterNavigation(NavigationItems, false);
      }
    });
  }

  fireOutClick() {
    let current_url = this.location.path();
    if (this.location['_baseHref']) {
      current_url = this.location['_baseHref'] + this.location.path();
    }
    const link = "a.nav-link[ href='" + current_url + "' ]";
    const ele = document.querySelector(link);
    if (ele !== null && ele !== undefined) {
      const parent = ele.parentElement;
      const up_parent = parent.parentElement.parentElement;
      const last_parent = up_parent.parentElement;
      if (parent.classList.contains('pcoded-hasmenu')) {
        parent.classList.add('pcoded-trigger');
        parent.classList.add('active');
      } else if (up_parent.classList.contains('pcoded-hasmenu')) {
        up_parent.classList.add('pcoded-trigger');
        up_parent.classList.add('active');
      } else if (last_parent.classList.contains('pcoded-hasmenu')) {
        last_parent.classList.add('pcoded-trigger');
        last_parent.classList.add('active');
      }
    }
  }

  private filterNavigation(items: NavigationItem[], isAdmin: boolean): NavigationItem[] {
    return items.flatMap((item) => {
      if (item.hidden || (item.adminOnly && !isAdmin)) {
        return [];
      }

      const children = item.children ? this.filterNavigation(item.children, isAdmin) : undefined;
      if (item.children && !children?.length) {
        return [];
      }

      return [{ ...item, children }];
    });
  }
}
