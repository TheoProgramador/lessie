import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  JobOpportunity,
  OpportunityDiscoveryService,
  OpportunitySearchResponse
} from 'src/app/services/opportunity-discovery.service';
import { SharedModule } from 'src/app/theme/shared/shared.module';

@Component({
  selector: 'app-opportunity-discovery',
  imports: [CommonModule, FormsModule, SharedModule],
  templateUrl: './opportunity-discovery.component.html',
  styleUrls: ['./opportunity-discovery.component.scss']
})
export class OpportunityDiscoveryComponent {
  private readonly opportunityService = inject(OpportunityDiscoveryService);

  query = '';
  location = '';
  limit = 20;
  loading = false;
  hasSearched = false;
  error = '';
  response: OpportunitySearchResponse | null = null;

  get results(): JobOpportunity[] {
    return this.response?.results ?? [];
  }

  search(): void {
    const query = this.query.trim();
    this.error = '';
    this.response = null;

    if (!query || this.loading) {
      return;
    }

    this.loading = true;
    this.hasSearched = true;
    this.opportunityService
      .search({
        query,
        location: this.location.trim() || undefined,
        limit: Math.min(Math.max(Number(this.limit) || 20, 1), 80)
      })
      .subscribe({
        next: (response) => {
          this.loading = false;
          this.response = response;
          if (!response.success) {
            this.error = response.error || 'Unable to run opportunity search.';
          }
        },
        error: () => {
          this.loading = false;
          this.error = 'Unable to run opportunity search.';
        }
      });
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.search();
    }
  }

  trackJob(_index: number, job: JobOpportunity): string {
    return job.resultKey || job.id || `${job.title}-${job.company}`;
  }

}
