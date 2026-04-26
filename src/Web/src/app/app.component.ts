import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { NotificationBellComponent } from './features/notifications/notification-bell.component';
import { ThemeService } from './core/theme/theme.service';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NotificationBellComponent],
  template: `
    <header class="app-header">
      <div class="header-left">
        <div class="logo" routerLink="/pipeline" style="cursor: pointer">
          <div class="logo-mark">P</div>
          PulseCRM
        </div>
        <div class="header-divider"></div>
        <nav class="header-nav">
          <a routerLink="/pipeline" routerLinkActive="active" class="nav-link">Pipeline</a>
          <a routerLink="/contacts" routerLinkActive="active" class="nav-link">Contacts</a>
        </nav>
      </div>

      <div class="header-right">
        <button class="icon-btn" (click)="theme.toggle()" [title]="theme.isLight() ? 'Switch to dark mode' : 'Switch to light mode'">
          @if (theme.isLight()) {
            <!-- Sun icon -->
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="5"/>
              <line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/>
              <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>
              <line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/>
              <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
            </svg>
          } @else {
            <!-- Moon icon -->
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
            </svg>
          }
        </button>

        <app-notification-bell />

        <div class="user-menu-container" style="position: relative;">
          <div class="user-chip" tabindex="0" (click)="profileOpen = !profileOpen" (blur)="profileOpen = false">
            <div class="user-avatar">AD</div>
            <span class="user-name">Alice Demo</span>
            <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" style="opacity:.4"><polyline points="6 9 12 15 18 9"/></svg>
          </div>

          @if (profileOpen) {
            <div class="profile-dropdown">
              <div class="dropdown-header">
                <strong>Alice Demo</strong>
                <span>alice&#64;pulsecrm.dev</span>
              </div>
              <div class="dropdown-divider"></div>
              <a class="dropdown-item" routerLink="/settings" (mousedown)="$event.preventDefault()">Profile Settings</a>
              <button class="dropdown-item text-red" (mousedown)="$event.preventDefault(); auth.logout()">Log out</button>
            </div>
          }
        </div>
      </div>
    </header>

    <main>
      <router-outlet />
    </main>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; height: 100vh; }

    .app-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0 20px;
      height: 52px;
      background: rgba(0,0,0,0.85);
      border-bottom: 1px solid var(--border);
      flex-shrink: 0;
      backdrop-filter: blur(12px);
      position: sticky;
      top: 0;
      z-index: 100;
      transition: background 200ms ease, border-color 200ms ease;
    }

    :host-context(body.light) .app-header {
      background: rgba(246,246,244,0.88);
    }

    .header-left { display: flex; align-items: center; }

    .logo {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: .9rem;
      font-weight: 700;
      color: var(--text);
      letter-spacing: -0.03em;
      white-space: nowrap;
    }
    .logo-mark {
      width: 24px; height: 24px;
      background: var(--accent);
      border-radius: 6px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: .7rem;
      font-weight: 800;
      color: white;
      flex-shrink: 0;
    }
    .header-divider {
      width: 1px; height: 20px;
      background: var(--border);
      margin: 0 16px;
      flex-shrink: 0;
    }
    .header-nav { display: flex; align-items: center; gap: 2px; }
    .nav-link {
      padding: 5px 10px;
      border-radius: 6px;
      font-size: .8rem;
      font-weight: 500;
      color: var(--text-3);
      transition: color 150ms, background 150ms;
      text-decoration: none;
    }
    .nav-link:hover { color: var(--text); background: var(--bg-hover); }
    .nav-link.active { color: var(--text); background: var(--bg-hover); }

    .header-right { display: flex; align-items: center; gap: 8px; }

    .icon-btn {
      width: 32px; height: 32px;
      background: none;
      border: 1px solid var(--border);
      border-radius: 6px;
      color: var(--text-2);
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      transition: background 150ms, border-color 150ms, color 150ms;
      flex-shrink: 0;
    }
    .icon-btn:hover { background: var(--bg-hover); border-color: var(--border-md); color: var(--text); }

    .user-chip {
      display: flex;
      align-items: center;
      gap: 7px;
      padding: 4px 8px 4px 4px;
      border: 1px solid var(--border);
      border-radius: 8px;
      cursor: pointer;
      background: none;
      color: var(--text);
      transition: background 150ms, border-color 150ms;
    }
    .user-chip:hover { background: var(--bg-hover); border-color: var(--border-md); }
    .user-avatar {
      width: 24px; height: 24px;
      border-radius: 50%;
      background: linear-gradient(135deg, var(--accent), #8b5cf6);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: .62rem;
      font-weight: 700;
      color: white;
      flex-shrink: 0;
    }
    .user-name { font-size: .8rem; font-weight: 500; color: var(--text-2); }

    main {
      flex: 1;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      background: var(--bg);
    }

    .user-menu-container { position: relative; }
    .profile-dropdown {
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      width: 200px;
      background: var(--bg-3);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      box-shadow: 0 10px 25px rgba(0,0,0,0.2);
      z-index: 200;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }
    .dropdown-header {
      padding: 12px 14px;
      background: var(--bg-2);
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .dropdown-header strong { font-size: .85rem; color: var(--text); font-weight: 600; }
    .dropdown-header span { font-size: .75rem; color: var(--text-3); }
    .dropdown-divider { height: 1px; background: var(--border); }
    .dropdown-item {
      padding: 10px 14px;
      font-size: .85rem;
      color: var(--text-2);
      cursor: pointer;
      text-decoration: none;
      transition: background 150ms, color 150ms;
      background: none;
      border: none;
      text-align: left;
      font-family: inherit;
    }
    .dropdown-item:hover {
      background: var(--bg-hover);
      color: var(--text);
    }
    .dropdown-item.text-red { color: var(--red); }
    .dropdown-item.text-red:hover { background: rgba(239, 68, 68, 0.1); color: var(--red); }
  `]
})
export class AppComponent {
  theme = inject(ThemeService);
  auth = inject(AuthService);
  profileOpen = false;
}
