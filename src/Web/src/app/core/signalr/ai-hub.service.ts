import { Injectable, signal, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { AuthService } from '../auth/auth.service';

@Injectable({ providedIn: 'root' })
export class AiHubService implements OnDestroy {
  private hub: signalR.HubConnection;
  private connected = false;

  summaryStreaming = signal<{ dealId: string; chunk: string } | null>(null);
  summaryCompleted = signal<{ dealId: string; summary: string; generatedAt: string } | null>(null);
  summaryFailed = signal<{ dealId: string; reason: string } | null>(null);

  constructor(private auth: AuthService) {
    this.hub = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/ai', { accessTokenFactory: () => this.auth.getToken() })
      .withAutomaticReconnect()
      .build();

    this.hub.on('SummaryStreaming', (data: { dealId: string; chunk: string }) => {
      this.summaryStreaming.set(data);
    });

    this.hub.on('SummaryCompleted', (data: { dealId: string; summary: string; generatedAt: string }) => {
      this.summaryCompleted.set(data);
    });

    this.hub.on('SummaryFailed', (data: { dealId: string; reason: string }) => {
      this.summaryFailed.set(data);
    });

    this.hub.onreconnected(() => {
      console.log('[AiHub] Reconnected');
    });

    this.hub.onclose(err => {
      if (err) {
        console.error('[AiHub] Connection closed with error:', err);
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
      console.error('[AiHub] Connection failed:', err);
      throw err;
    }
  }

  async watchDeal(dealId: string): Promise<void> {
    if (this.hub.state !== signalR.HubConnectionState.Connected) {
      await this.connect();
    }
    await this.hub.invoke('WatchDeal', dealId);
  }

  async unwatchDeal(dealId: string): Promise<void> {
    if (this.hub.state !== signalR.HubConnectionState.Connected) return;
    await this.hub.invoke('UnwatchDeal', dealId);
  }

  ngOnDestroy(): void {
    this.hub.stop();
  }
}
