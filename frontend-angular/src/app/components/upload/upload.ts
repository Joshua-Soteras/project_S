import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-upload',
  imports: [FormsModule],
  templateUrl: './upload.html',
  styleUrl: './upload.scss',
})
export class Upload {
  private api = inject(ApiService);
  private router = inject(Router);

  selectedFile: File | null = null;
  surveyName = '';
  uploading = false;
  error: string | null = null;

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.error = null;
  }

  onSubmit(): void {
    if (!this.selectedFile) {
      this.error = 'Please select a CSV file.';
      return;
    }

    this.uploading = true;
    this.error = null;

    this.api.uploadSurvey(this.selectedFile, this.surveyName || undefined).subscribe({
      next: (result) => {
        this.uploading = false;
        // Navigate to the dashboard for the newly uploaded survey
        this.router.navigate(['/surveys', result.id]);
      },
      error: (err) => {
        this.uploading = false;
        this.error = err.error?.error ?? 'Upload failed. Please try again.';
      },
    });
  }
}
