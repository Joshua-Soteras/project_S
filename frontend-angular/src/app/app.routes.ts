import { Routes } from '@angular/router';
import { SurveyList } from './components/survey-list/survey-list';
import { Upload } from './components/upload/upload';
import { Dashboard } from './components/dashboard/dashboard';

export const routes: Routes = [
  { path: '',           component: SurveyList },
  { path: 'upload',     component: Upload },
  { path: 'surveys/:id', component: Dashboard },
  { path: '**',         redirectTo: '' },
];
