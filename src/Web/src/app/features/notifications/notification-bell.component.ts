import { Component, OnInit, signal, inject, effect, HostListener } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NotificationsHubService } from '../../core/signalr/notifications-hub.service';
import { NotificationsService, Notification } from '../../core/api/notifications.service';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [DatePipe],
  template: `
    <div class="bell-wrap">
      <button class="icon-btn" (click)="toggleOpen()" [attr.aria-label]="'Notifications'">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
          <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
        </svg>
        @if (unreadCount() > 0) {
          <span class="badge">{{ unreadCount() > 99 ? '99+' : unreadCount() }}</span>
        }
      </button>

      @if (open()) {
        <div class="dropdown">
          <div class="dropdown-header">
            <span class="dropdown-title">Notifications</span>
            @if (unreadCount() > 0) {
              <button class="mark-all-btn" (click)="markAllRead()">Mark all read</button>
            }
          </div>
          <div class="dropdown-body">
            @for (n of notifications(); track n.id) {
              <div class="notif-item" [class.unread]="!n.readAt" (click)="markRead(n)">
                <div class="notif-content">
                  <p class="notif-message">{{ n.message }}</p>
                  <div class="notif-meta">
                    <span class="notif-type">{{ n.type }}</span>
                    <span class="notif-time">{{ n.createdAt | date:'shortTime' }}</span>
                  </div>
                </div>
                @if (!n.readAt) {
                  <span class="unread-dot"></span>
                }
              </div>
            } @empty {
              <div class="empty-state">
                <p>All caught up!</p>
                <span>No notifications.</span>
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .bell-wrap { position: relative; }

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
      position: relative;
    }
    .icon-btn:hover { background: var(--bg-hover); border-color: var(--border-md); color: var(--text); }

    .badge {
      position: absolute;
      top: -5px; right: -5px;
      background: var(--accent);
      color: white;
      font-size: .6rem;
      font-weight: 700;
      border-radius: 8px;
      min-width: 16px;
      height: 16px;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 0 4px;
      border: 2px solid var(--bg);
    }

    .dropdown {
      position: absolute;
      right: 0;
      top: calc(100% + 8px);
      background: var(--bg-3);
      border: 1px solid var(--border-md);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow);
      min-width: 330px;
      max-width: 370px;
      z-index: 200;
      overflow: hidden;
    }
    .dropdown-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 11px 14px;
      border-bottom: 1px solid var(--border);
      background: var(--bg-4);
    }
    .dropdown-title { font-weight: 600; font-size: .82rem; color: var(--text); }
    .mark-all-btn {
      background: none; border: none;
      color: var(--accent);
      font-size: .75rem; font-weight: 500;
      cursor: pointer; font-family: inherit;
    }
    .mark-all-btn:hover { text-decoration: underline; }

    .dropdown-body { max-height: 360px; overflow-y: auto; }
    .dropdown-body::-webkit-scrollbar { width: 4px; }
    .dropdown-body::-webkit-scrollbar-thumb { background: var(--border-md); border-radius: 2px; }

    .notif-item {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      padding: 11px 14px;
      border-bottom: 1px solid var(--border);
      cursor: pointer;
      transition: background 100ms;
    }
    .notif-item:last-child { border-bottom: none; }
    .notif-item:hover { background: var(--bg-hover); }
    .notif-item.unread { background: rgba(99,102,241,0.06); }
    .notif-item.unread:hover { background: rgba(99,102,241,0.1); }

    .notif-content { flex: 1; min-width: 0; }
    .notif-message { margin: 0 0 5px; font-size: .82rem; color: var(--text); line-height: 1.4; }
    .notif-meta { display: flex; align-items: center; gap: 8px; }
    .notif-type {
      font-size: .68rem; color: var(--accent);
      background: var(--accent-dim);
      padding: 1px 6px; border-radius: 4px; font-weight: 500;
    }
    .notif-time { font-size: .7rem; color: var(--text-3); }
    .unread-dot {
      width: 6px; height: 6px;
      background: var(--accent);
      border-radius: 50%;
      flex-shrink: 0;
      margin-top: 7px;
      box-shadow: 0 0 5px var(--accent-glow);
    }
    .empty-state {
      padding: 32px 16px;
      text-align: center;
      color: var(--text-3);
    }
    .empty-state p { font-weight: 600; margin-bottom: 4px; color: var(--text-2); }
    .empty-state span { font-size: .82rem; }
  `]
})
export class NotificationBellComponent implements OnInit {
  private hub = inject(NotificationsHubService);
  private notifService = inject(NotificationsService);

  notifications = signal<Notification[]>([]);
  open = signal(false);
  unreadCount = signal(0);

  constructor() {
    effect(() => {
      const n = this.hub.notificationReceived();
      if (n) { this.notifications.update(l => [n, ...l]); if (!n.readAt) this.unreadCount.update(c => c + 1); }
    });
  }

  async ngOnInit() {
    try {
      await this.hub.connect();
      const list = await this.notifService.getNotifications();
      this.notifications.set(list);
      this.unreadCount.set(list.filter((n: Notification) => !n.readAt).length);
    } catch {}
  }

  toggleOpen() { this.open.update(v => !v); }

  async markRead(n: Notification) {
    if (n.readAt) return;
    try {
      await this.notifService.markRead(n.id);
      this.notifications.update(l => l.map(x => x.id === n.id ? { ...x, readAt: new Date().toISOString() } : x));
      this.unreadCount.update(c => Math.max(0, c - 1));
    } catch {}
  }

  async markAllRead() {
    try {
      await this.notifService.markAllRead();
      this.notifications.update(l => l.map(n => ({ ...n, readAt: n.readAt ?? new Date().toISOString() })));
      this.unreadCount.set(0);
    } catch {}
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(e: Event) {
    if (!(e.target as HTMLElement).closest('app-notification-bell')) this.open.set(false);
  }
}
