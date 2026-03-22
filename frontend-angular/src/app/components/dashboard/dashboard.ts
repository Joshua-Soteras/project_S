import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { SurveyDetail } from '../../models/survey.model';

const TERMINAL_STATUSES = new Set(['complete', 'error']);
const POLL_INTERVAL_MS = 2000;

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, DatePipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);

  survey: SurveyDetail | null = null;
  loading = true;
  error: string | null = null;

  private pollTimer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.api.getSurvey(id).subscribe({
      next: (survey) => {
        this.survey = survey;
        this.loading = false;
        // If the survey is still being processed, poll until it reaches a
        // terminal state (complete or error).
        if (!TERMINAL_STATUSES.has(survey.status)) {
          this.startPolling(id);
        }
      },
      error: () => {
        this.error = 'Survey not found or failed to load.';
        this.loading = false;
      },
    });
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  private startPolling(id: number): void {
    this.pollTimer = setInterval(() => {
      this.api.getSurvey(id).subscribe({
        next: (survey) => {
          this.survey = survey;
          if (TERMINAL_STATUSES.has(survey.status)) {
            this.stopPolling();
          }
        },
        // Swallow poll errors — the initial load already showed if the
        // survey exists. A transient failure here just delays the update.
        error: () => {},
      });
    }, POLL_INTERVAL_MS);
  }

  private stopPolling(): void {
    if (this.pollTimer !== null) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  /** Returns a Tailwind badge class string based on the survey status. */
  statusClass(status: string): string {
    const map: Record<string, string> = {
      complete:   'bg-green-100 text-green-700',
      processing: 'bg-yellow-100 text-yellow-700',
      queued:     'bg-gray-100 text-gray-600',
      error:      'bg-red-100 text-red-700',
    };
    return map[status] ?? 'bg-gray-100 text-gray-600';
  }

  /** Formats a 0–1 float as a percentage string. */
  pct(value: number): string {
    return `${(value * 100).toFixed(1)}%`;
  }
}
