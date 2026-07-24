import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';

import { authGuard } from './services/auth.guard';
import { adminGuard } from './services/admin.guard';
import { paymentGuard } from './services/payment.guard';
import { AdminComponent } from './theme/layout/admin/admin.component';
import { GuestComponent } from './theme/layout/guest/guest.component';

const routes: Routes = [
  {
    path: '',
    component: GuestComponent,
    children: [
      {
        path: '',
        loadComponent: () => import('./pages/landing/landing.component').then((c) => c.LandingComponent)
      },
      {
        path: 'comprar-creditos',
        loadComponent: () => import('./pages/credits/credits.component').then((c) => c.CreditsComponent)
      },
      {
        path: 'login',
        loadComponent: () => import('./pages/authentication/auth-signin/auth-signin.component').then((c) => c.AuthSigninComponent)
      }
    ]
  },
  {
    path: '',
    component: AdminComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'payment-required',
        loadComponent: () => import('./pages/payment-required/payment-required.component').then((c) => c.PaymentRequiredComponent)
      },
      {
        path: 'credits',
        loadComponent: () => import('./pages/credits/credits.component').then((c) => c.CreditsComponent)
      },
      {
        path: 'dashboard',
        canActivate: [paymentGuard],
        loadComponent: () => import('./demo/dashboard/dashboard.component').then((c) => c.DashboardComponent)
      },
      {
        path: 'chatbot',
        canActivate: [adminGuard],
        loadComponent: () => import('./pages/chatbot/chatbot.component').then((c) => c.ChatbotComponent)
      },
      {
        path: 'people-discovery',
        canActivate: [paymentGuard],
        data: { mode: 'people' },
        loadComponent: () => import('./pages/people-discovery/people-discovery.component').then((c) => c.PeopleDiscoveryComponent)
      },
      {
        path: 'people-discovery/posts',
        canActivate: [paymentGuard],
        data: { mode: 'posts' },
        loadComponent: () => import('./pages/people-discovery/people-discovery.component').then((c) => c.PeopleDiscoveryComponent)
      },
      {
        path: 'people-discovery/jobs',
        canActivate: [paymentGuard],
        data: { mode: 'jobs' },
        loadComponent: () => import('./pages/people-discovery/people-discovery.component').then((c) => c.PeopleDiscoveryComponent)
      },
      {
        path: 'opportunity-discovery',
        canActivate: [paymentGuard],
        loadComponent: () =>
          import('./pages/opportunity-discovery/opportunity-discovery.component').then((c) => c.OpportunityDiscoveryComponent)
      },
      {
        path: 'resume-improvements',
        canActivate: [paymentGuard],
        loadComponent: () =>
          import('./pages/resume-improvements/resume-improvements.component').then((c) => c.ResumeImprovementsComponent)
      },
      {
        path: 'interview-analysis',
        canActivate: [paymentGuard],
        loadComponent: () =>
          import('./pages/interview-analysis/interview-analysis.component').then((c) => c.InterviewAnalysisComponent)
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'login'
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule {}
