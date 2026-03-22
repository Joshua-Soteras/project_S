import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { SurveyDetail } from '../../models/survey.model';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, DatePipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);

  survey: SurveyDetail | null = null;
  loading = true;
  error: string | null = null;

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.api.getSurvey(id).subscribe({
      next: (survey) => {
        this.survey = survey;
        this.loading = false;
      },
      error: () => {
        this.error = 'Survey not found or failed to load.';
        this.loading = false;
      },
    });
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
