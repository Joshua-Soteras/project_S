import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { Survey } from '../../models/survey.model';

@Component({
  selector: 'app-survey-list',
  imports: [RouterLink, DatePipe],
  templateUrl: './survey-list.html',
  styleUrl: './survey-list.scss',
})
export class SurveyList implements OnInit {
  private api = inject(ApiService);

  surveys: Survey[] = [];
  loading = true;
  error: string | null = null;

  ngOnInit(): void {
    this.api.getSurveys().subscribe({
      next: (surveys) => {
        this.surveys = surveys;
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to load surveys. Is the API running?';
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
}
