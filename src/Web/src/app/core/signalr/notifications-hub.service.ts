import { Injectable, signal, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { AuthService } from '../auth/auth.service';

@Injectable({ providedIn: 'root' })
export class NotificationsHubService implements OnDestroy {
  private hub: signalR.HubConnection;
  private connected = false;

  notificationReceived = signal<any>(null);
  notificationRead = signal<any>(null);
  allNotificationsRead = signal<boolean>(false);

  constructor(private auth: AuthService) {
    this.hub = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', { accessTokenFactory: () => this.auth.getToken() })
      .withAutomaticReconnect()
      .build();

    this.hub.on('NotificationReceived', data => this.notificationReceived.set(data));
    this.hub.on('NotificationRead', data => this.notificationRead.set(data));
    this.hub.on('AllNotificationsRead', () => this.allNotificationsRead.set(true));

    this.hub.onreconnected(() => {
      console.log('[NotificationsHub] Reconnected');
    });

    this.hub.onclose(err => {
      if (err) {
        console.error('[NotificationsHub] Connection closed with error:', err);
      }
      this.connected = false;
    });
  }

  async connect(): Promise<void> {
    if (this.connected || this.hub.state === signalR.HubConnectionState.Connected) return;
    try {
      await this.hub.start();
      this.connected = true;
    } catch (err) {
      console.error('[NotificationsHub] Connection failed:', err);
      throw err;
    }
  }

  async disconnect(): Promise<void> {
    if (this.hub.state !== signalR.HubConnectionState.Connected) return;
    await this.hub.stop();
    this.connected = false;
  }

  ngOnDestroy(): void {
    this.hub.stop();
  }
}
