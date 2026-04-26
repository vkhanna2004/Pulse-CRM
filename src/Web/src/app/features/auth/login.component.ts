import { Component, OnInit } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  standalone: true,
  template: `
    <div class="login-container">
      <div class="login-card">
        <div class="login-logo">
          <span class="logo-text">PulseCRM</span>
        </div>
        <h1 class="login-title">Welcome back</h1>
        <p class="login-subtitle">Sign in to continue to your workspace</p>
        <button class="login-btn" (click)="login()">
          Sign in with Keycloak
        </button>
      </div>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      background: var(--color-bg);
    }
    .login-card {
      background: var(--color-surface);
      border-radius: 16px;
      padding: 48px 40px;
      text-align: center;
      box-shadow: 0 4px 24px rgba(0,0,0,0.08);
      width: 100%;
      max-width: 400px;
    }
    .login-logo {
      margin-bottom: 24px;
    }
    .logo-text {
      font-size: 1.75rem;
      font-weight: 700;
      color: var(--color-primary);
      letter-spacing: -0.02em;
    }
    .login-title {
      font-size: 1.5rem;
      font-weight: 700;
      color: var(--color-text);
      margin-bottom: 8px;
    }
    .login-subtitle {
      color: var(--color-text-muted);
      margin-bottom: 32px;
      font-size: 0.95rem;
    }
    .login-btn {
      width: 100%;
      padding: 14px 24px;
      background: var(--color-primary);
      color: white;
      border: none;
      border-radius: 8px;
      font-size: 1rem;
      font-weight: 600;
      cursor: pointer;
      transition: background 150ms ease;
    }
    .login-btn:hover {
      background: var(--color-primary-dark);
    }
  `]
})
export class LoginComponent implements OnInit {
  constructor(private auth: AuthService) {}

  ngOnInit(): void {
    if (this.auth.isLoggedIn()) {
      window.location.href = '/pipeline';
    }
  }

  login(): void {
    this.auth.login();
  }
}
