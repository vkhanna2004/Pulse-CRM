import { AuthConfig } from 'angular-oauth2-oidc';

export const authConfig: AuthConfig = {
  issuer: 'http://localhost:8080/realms/pulsecrm',
  redirectUri: window.location.origin,
  clientId: 'pulsecrm-spa',
  responseType: 'code',
  scope: 'openid profile email',
  useSilentRefresh: true,
  silentRefreshTimeout: 5000,
  sessionChecksEnabled: false,
  showDebugInformation: false,
  requireHttps: false
};
