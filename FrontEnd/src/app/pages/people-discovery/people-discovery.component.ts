import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import {
  PeopleDiscoveryJob,
  PeopleDiscoveryJobSearchRequest,
  PeopleDiscoveryJobSearchResponse,
  PeopleDiscoveryJobStreamEvent,
  PeopleDiscoveryPerson,
  PeopleDiscoveryProgressEvent,
  PeopleDiscoveryStreamEvent,
  PeopleDiscoverySearchResponse,
  PeopleDiscoveryService
} from 'src/app/services/people-discovery.service';
import { SharedModule } from 'src/app/theme/shared/shared.module';

@Component({
  selector: 'app-people-discovery',
  imports: [CommonModule, FormsModule, SharedModule],
  templateUrl: './people-discovery.component.html',
  styleUrls: ['./people-discovery.component.scss']
})
export class PeopleDiscoveryComponent implements OnDestroy, OnInit {
  private readonly peopleDiscoveryService = inject(PeopleDiscoveryService);
  private readonly route = inject(ActivatedRoute);
  private readonly changeDetectorRef = inject(ChangeDetectorRef);
  private searchSubscription?: Subscription;
  private postsSubscription?: Subscription;
  private jobsSubscription?: Subscription;
  readonly mode = this.route.snapshot.data['mode'] as 'people' | 'posts' | 'jobs' | undefined;

  readonly examples = ['.NET recruiters Brazil remote', 'Angular developers S\u00e3o Paulo', 'AWS recruiters LATAM'];
  readonly postExamples = ['recrutando .NET remoto email', 'hiring angular whatsapp', 'vaga backend contato'];
  readonly datePostedOptions = [
    { value: '', label: 'Any date' },
    { value: 'past_hour', label: 'Past hour' },
    { value: 'past_24_hours', label: 'Past 24 hours' },
    { value: 'past_week', label: 'Past week' },
    { value: 'past_month', label: 'Past month' }
  ];
  readonly jobTypeOptions = [
    { value: '', label: 'Any type' },
    { value: 'full_time', label: 'Full time' },
    { value: 'part_time', label: 'Part time' },
    { value: 'contract', label: 'Contract' },
    { value: 'temporary', label: 'Temporary' },
    { value: 'volunteer', label: 'Volunteer' },
    { value: 'internship', label: 'Internship' },
    { value: 'other', label: 'Other' }
  ];
  readonly experienceLevelOptions = [
    { value: '', label: 'Any level' },
    { value: 'internship', label: 'Internship' },
    { value: 'entry', label: 'Entry' },
    { value: 'associate', label: 'Associate' },
    { value: 'mid_senior', label: 'Mid senior' },
    { value: 'director', label: 'Director' },
    { value: 'executive', label: 'Executive' }
  ];
  readonly workTypeOptions = [
    { value: '', label: 'Any work type' },
    { value: 'remote', label: 'Remote' },
    { value: 'hybrid', label: 'Hybrid' },
    { value: 'on_site', label: 'On site' }
  ];
  readonly sortOptions = [
    { value: '', label: 'Default' },
    { value: 'relevance', label: 'Relevance' },
    { value: 'date', label: 'Date' }
  ];

  query = '';
  loading = false;
  error = '';
  hasSearched = false;
  response: PeopleDiscoverySearchResponse | null = null;
  progressEvents: PeopleDiscoveryProgressEvent[] = [];
  postQuery = '';
  postLocation = 'Brazil';
  postsLoading = false;
  postsError = '';
  postsHasSearched = false;
  postsResponse: PeopleDiscoverySearchResponse | null = null;
  postsProgressEvents: PeopleDiscoveryProgressEvent[] = [];
  markingResumeSent = new Set<string>();
  jobSearch: PeopleDiscoveryJobSearchRequest = {
    keywords: '',
    location: 'Brazil',
    maxPages: 5,
    datePosted: '',
    jobType: '',
    experienceLevel: '',
    workType: '',
    easyApply: false,
    sortBy: ''
  };
  jobsLoading = false;
  jobsError = '';
  jobsHasSearched = false;
  jobsResponse: PeopleDiscoveryJobSearchResponse | null = null;
  jobsProgressEvents: PeopleDiscoveryProgressEvent[] = [];

  get results(): PeopleDiscoveryPerson[] {
    return this.response?.results ?? [];
  }

  get pageTitle(): string {
    if (this.mode === 'posts') {
      return 'Post Search';
    }

    if (this.mode === 'jobs') {
      return 'Job Search';
    }

    return 'People Search';
  }

  get pageDescription(): string {
    if (this.mode === 'posts') {
      return 'Find hiring posts and direct contact details from LinkedIn posts.';
    }

    if (this.mode === 'jobs') {
      return 'Search LinkedIn job listings with dedicated filters.';
    }

    return 'Find professionals, recruiters and relevant contacts using connected discovery tools.';
  }

  get showPeopleSearch(): boolean {
    return this.mode !== 'posts' && this.mode !== 'jobs';
  }

  get showPostSearch(): boolean {
    return this.mode === 'posts';
  }

  get showJobSearch(): boolean {
    return this.mode === 'jobs';
  }

  get jobResults(): PeopleDiscoveryJob[] {
    return this.jobsResponse?.results ?? [];
  }

  get postResults(): PeopleDiscoveryPerson[] {
    return this.postsResponse?.results ?? [];
  }

  ngOnInit(): void {
    if (this.mode !== 'jobs') {
      return;
    }

    const keywords = (this.route.snapshot.queryParamMap.get('keywords') || this.route.snapshot.queryParamMap.get('q') || '').trim();
    if (!keywords) {
      return;
    }

    this.jobSearch = {
      ...this.jobSearch,
      keywords
    };

    if (this.route.snapshot.queryParamMap.get('autoSearch') === '1') {
      setTimeout(() => this.searchJobs());
    }
  }

  search(): void {
    const query = this.query.trim();
    this.error = '';
    this.response = null;
    this.progressEvents = [];

    if (!query || this.loading) {
      return;
    }

    this.loading = true;
    this.hasSearched = true;
    this.searchSubscription?.unsubscribe();

    this.searchSubscription = this.peopleDiscoveryService.searchWithProgress(query).subscribe({
      next: (event) => {
        if (event.type === 'progress') {
          this.progressEvents = [...this.progressEvents, event.data].slice(-12);
          this.refreshView();
          return;
        }

        if (event.type === 'result') {
          this.loading = false;
          this.response = event.data;

          if (!event.data.success) {
            this.error = event.data.error || 'Unable to run People Discovery.';
          }
          this.refreshView();
          return;
        }

        this.loading = false;
        this.error = event.data.message || 'Unable to run People Discovery.';
        this.refreshView();
      },
      error: () => {
        this.loading = false;
        this.error = 'Unable to run People Discovery.';
        this.refreshView();
      },
      complete: () => {
        if (this.loading) {
          this.loading = false;
          this.refreshView();
        }
      }
    });
  }

  ngOnDestroy(): void {
    this.searchSubscription?.unsubscribe();
    this.postsSubscription?.unsubscribe();
    this.jobsSubscription?.unsubscribe();
  }

  runExample(example: string): void {
    this.query = example;
    this.search();
  }

  runPostExample(example: string): void {
    this.postQuery = example;
    this.searchPosts();
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.search();
    }
  }

  searchPosts(): void {
    const query = this.postQuery.trim();
    this.postsError = '';
    this.postsResponse = null;
    this.postsProgressEvents = [];

    if (!query || this.postsLoading) {
      return;
    }

    this.postsLoading = true;
    this.postsHasSearched = true;
    this.postsSubscription?.unsubscribe();

    this.postsSubscription = this.peopleDiscoveryService.searchPostsWithProgress(query, this.cleanFilter(this.postLocation)).subscribe({
      next: (event) => {
        this.handlePostsStreamEvent(event);
        this.refreshView();
      },
      error: () => {
        this.postsLoading = false;
        this.postsError = 'Unable to run LinkedIn Posts search.';
        this.refreshView();
      },
      complete: () => {
        if (this.postsLoading) {
          this.postsLoading = false;
          this.refreshView();
        }
      }
    });
  }

  onPostsKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.searchPosts();
    }
  }

  markResumeSent(result: PeopleDiscoveryPerson | PeopleDiscoveryJob, list: 'people' | 'posts' | 'jobs' = 'posts'): void {
    if (!result.resultKey || this.markingResumeSent.has(result.resultKey)) {
      return;
    }

    this.markingResumeSent = new Set([...this.markingResumeSent, result.resultKey]);
    this.peopleDiscoveryService.markResumeSent(result.resultKey).subscribe({
      next: (response) => {
        this.markingResumeSent.delete(result.resultKey);
        this.markingResumeSent = new Set(this.markingResumeSent);
        if (response.success) {
          this.removeResumeSentResult(result.resultKey, list);
          return;
        }

        this.setResumeSentError(list);
      },
      error: () => {
        this.markingResumeSent.delete(result.resultKey);
        this.markingResumeSent = new Set(this.markingResumeSent);
        this.setResumeSentError(list);
      }
    });
  }

  searchJobs(): void {
    const keywords = this.jobSearch.keywords.trim();
    this.jobsError = '';
    this.jobsResponse = null;
    this.jobsProgressEvents = [];

    if (!keywords || this.jobsLoading) {
      return;
    }

    this.jobsLoading = true;
    this.jobsHasSearched = true;
    this.jobsSubscription?.unsubscribe();

    this.jobsSubscription = this.peopleDiscoveryService.searchJobsWithProgress({
      ...this.jobSearch,
      keywords,
      location: this.cleanFilter(this.jobSearch.location),
      datePosted: this.cleanFilter(this.jobSearch.datePosted),
      jobType: this.cleanFilter(this.jobSearch.jobType),
      experienceLevel: this.cleanFilter(this.jobSearch.experienceLevel),
      workType: this.cleanFilter(this.jobSearch.workType),
      sortBy: this.cleanFilter(this.jobSearch.sortBy),
      maxPages: Math.min(Math.max(Number(this.jobSearch.maxPages) || 1, 1), 5)
    }).subscribe({
      next: (event) => {
        this.handleJobsStreamEvent(event);
        this.refreshView();
      },
      error: () => {
        this.jobsLoading = false;
        this.jobsError = 'Unable to run LinkedIn Jobs search.';
        this.refreshView();
      },
      complete: () => {
        if (this.jobsLoading) {
          this.jobsLoading = false;
          this.refreshView();
        }
      }
    });
  }

  onJobsKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.searchJobs();
    }
  }

  trackPerson(_index: number, person: PeopleDiscoveryPerson): string {
    return person.resultKey || person.profileUrl || `${person.name}-${person.company}`;
  }

  trackPost(_index: number, person: PeopleDiscoveryPerson): string {
    return person.resultKey || person.profileUrl || `${person.name}-${person.title}`;
  }

  trackProgress(index: number): number {
    return index;
  }

  trackJob(_index: number, job: PeopleDiscoveryJob): string {
    return job.resultKey || job.jobUrl || job.jobId || `${job.title}-${job.company}`;
  }

  activityClass(event: PeopleDiscoveryProgressEvent): string {
    const level = event.level?.toLowerCase() || 'info';
    return `activity-${level}`;
  }

  shouldShowPeopleCount(event: PeopleDiscoveryProgressEvent): boolean {
    return event.level?.toLowerCase() !== 'error' && event.peopleCount !== null && event.peopleCount !== undefined;
  }

  private handlePostsStreamEvent(event: PeopleDiscoveryStreamEvent): void {
    if (event.type === 'progress') {
      this.postsProgressEvents = [...this.postsProgressEvents, event.data].slice(-12);
      return;
    }

    if (event.type === 'result') {
      this.postsLoading = false;
      this.postsResponse = event.data;
      if (!event.data.success) {
        this.postsError = event.data.error || 'Unable to run LinkedIn Posts search.';
      }
      return;
    }

    this.postsLoading = false;
    this.postsError = event.data.message || 'Unable to run LinkedIn Posts search.';
  }

  private handleJobsStreamEvent(event: PeopleDiscoveryJobStreamEvent): void {
    if (event.type === 'progress') {
      this.jobsProgressEvents = [...this.jobsProgressEvents, event.data].slice(-12);
      return;
    }

    if (event.type === 'result') {
      this.jobsLoading = false;
      this.jobsResponse = event.data;
      if (!event.data.success) {
        this.jobsError = event.data.error || 'Unable to run LinkedIn Jobs search.';
      }
      return;
    }

    this.jobsLoading = false;
    this.jobsError = event.data.message || 'Unable to run LinkedIn Jobs search.';
  }

  private removeResumeSentResult(resultKey: string, list: 'people' | 'posts' | 'jobs'): void {
    if (list === 'people' && this.response) {
      this.response = {
        ...this.response,
        results: this.response.results.filter((item) => item.resultKey !== resultKey)
      };
      return;
    }

    if (list === 'jobs' && this.jobsResponse) {
      this.jobsResponse = {
        ...this.jobsResponse,
        results: this.jobsResponse.results.filter((item) => item.resultKey !== resultKey)
      };
      return;
    }

    if (this.postsResponse) {
      this.postsResponse = {
        ...this.postsResponse,
        results: this.postsResponse.results.filter((item) => item.resultKey !== resultKey)
      };
    }
  }

  private setResumeSentError(list: 'people' | 'posts' | 'jobs'): void {
    if (list === 'people') {
      this.error = 'Unable to mark resume as sent.';
      return;
    }

    if (list === 'jobs') {
      this.jobsError = 'Unable to mark resume as sent.';
      return;
    }

    this.postsError = 'Unable to mark resume as sent.';
  }

  private cleanFilter(value?: string): string | undefined {
    return value?.trim() || undefined;
  }

  private refreshView(): void {
    this.changeDetectorRef.detectChanges();
  }
}
