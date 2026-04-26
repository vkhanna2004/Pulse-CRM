import { Injectable, signal, computed } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';
import { authConfig } from './auth.config';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _isLoggedIn = signal(false);
  isLoggedIn = computed(() => this._isLoggedIn());

  constructor(private oauthService: OAuthService) {
    this.oauthService.configure(authConfig);
  }

  /** Called by APP_INITIALIZER before app bootstraps */
  async initialize(): Promise<void> {
    await this.oauthService.loadDiscoveryDocumentAndTryLogin();
    this._isLoggedIn.set(this.oauthService.hasValidAccessToken());
    if (this.oauthService.hasValidAccessToken()) {
      this.oauthService.setupAutomaticSilentRefresh();
    }
  }

  login(): void { this.oauthService.initCodeFlow(); }
  logout(): void { this.oauthService.logOut(); }

  getToken(): string { return this.oauthService.getAccessToken() ?? ''; }

  get userId(): string {
    const claims = this.oauthService.getIdentityClaims() as Record<string, string>;
    return claims?.['sub'] ?? '';
  }

  get tenantId(): string {
    const claims = this.oauthService.getIdentityClaims() as Record<string, string>;
    return claims?.['tenant_id'] ?? '';
  }
}
