import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../core/auth/auth.service';
import { ThemeService } from '../../core/theme/theme.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="settings-container">
      <div class="settings-header">
        <h1>Profile Settings</h1>
        <p class="subtitle">Manage your account preferences and application settings.</p>
      </div>

      <div class="settings-card">
        <div class="card-header">
          <h2>Personal Information</h2>
        </div>
        <div class="card-body">
          <div class="info-group">
            <span class="label">Avatar</span>
            <div class="user-avatar-large">AD</div>
          </div>
          <div class="info-group">
            <span class="label">Name</span>
            <span class="value">Alice Demo</span>
          </div>
          <div class="info-group">
            <span class="label">Email</span>
            <span class="value">alice&#64;pulsecrm.dev</span>
          </div>
          <div class="info-group">
            <span class="label">User ID (Keycloak)</span>
            <span class="value uid">{{ auth.userId || 'Not authenticated' }}</span>
          </div>
        </div>
      </div>

      <div class="settings-card">
        <div class="card-header">
          <h2>Application Preferences</h2>
        </div>
        <div class="card-body">
          <div class="pref-row">
            <div class="pref-info">
              <h3>Appearance</h3>
              <p>Toggle between dark and light themes for the application interface.</p>
            </div>
            <button class="btn btn-outline" (click)="theme.toggle()">
              Switch to {{ theme.isLight() ? 'Dark' : 'Light' }} Mode
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .settings-container {
      padding: 32px;
      max-width: 800px;
      margin: 0 auto;
      width: 100%;
    }
    
    .settings-header {
      margin-bottom: 32px;
    }
    
    .settings-header h1 {
      font-size: 1.75rem;
      font-weight: 600;
      color: var(--text);
      margin: 0 0 8px 0;
    }
    
    .subtitle {
      color: var(--text-2);
      font-size: 1rem;
      margin: 0;
    }
    
    .settings-card {
      background: var(--bg-2);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      margin-bottom: 24px;
      overflow: hidden;
    }
    
    .card-header {
      padding: 16px 24px;
      border-bottom: 1px solid var(--border);
      background: var(--bg-3);
    }
    
    .card-header h2 {
      margin: 0;
      font-size: 1.1rem;
      font-weight: 600;
      color: var(--text);
    }
    
    .card-body {
      padding: 24px;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    
    .info-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    
    .info-group .label {
      font-size: 0.85rem;
      color: var(--text-3);
      font-weight: 500;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
    
    .info-group .value {
      font-size: 1rem;
      color: var(--text);
      font-weight: 500;
    }
    
    .info-group .value.uid {
      font-family: monospace;
      color: var(--text-2);
      font-size: 0.9rem;
    }
    
    .user-avatar-large {
      width: 48px;
      height: 48px;
      border-radius: 50%;
      background: linear-gradient(135deg, var(--primary), #818cf8);
      color: white;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: bold;
      font-size: 1.1rem;
    }
    
    .pref-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    
    .pref-info h3 {
      margin: 0 0 4px 0;
      font-size: 1rem;
      color: var(--text);
    }
    
    .pref-info p {
      margin: 0;
      font-size: 0.85rem;
      color: var(--text-2);
    }
    
    .btn {
      padding: 8px 16px;
      border-radius: var(--radius);
      font-weight: 500;
      cursor: pointer;
      border: 1px solid transparent;
      transition: all 150ms;
    }
    
    .btn-outline {
      background: transparent;
      border-color: var(--border);
      color: var(--text);
    }
    
    .btn-outline:hover {
      background: var(--bg-hover);
      border-color: var(--text-3);
    }
  `]
})
export class SettingsComponent {
  auth = inject(AuthService);
  theme = inject(ThemeService);
}
