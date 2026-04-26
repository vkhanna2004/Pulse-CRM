import { Injectable, signal, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { AuthService } from '../auth/auth.service';

@Injectable({ providedIn: 'root' })
export class PipelineHubService implements OnDestroy {
  private hub: signalR.HubConnection;

  dealMoved = signal<any>(null);
  dealCreated = signal<any>(null);
  dealUpdated = signal<any>(null);
  dealScoreUpdated = signal<any>(null);
  activityAdded = signal<any>(null);
  presenceChanged = signal<any>(null);

  constructor(private auth: AuthService) {
    this.hub = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/pipeline', { accessTokenFactory: () => this.auth.getToken() })
      .withAutomaticReconnect()
      .build();

    this.hub.on('DealMoved', data => this.dealMoved.set(data));
    this.hub.on('DealCreated', data => this.dealCreated.set(data));
    this.hub.on('DealUpdated', data => this.dealUpdated.set(data));
    this.hub.on('DealScoreUpdated', data => this.dealScoreUpdated.set(data));
    this.hub.on('ActivityAdded', data => this.activityAdded.set(data));
    this.hub.on('PresenceChanged', data => this.presenceChanged.set(data));
  }

  async connect(): Promise<void> {
    if (this.hub.state === signalR.HubConnectionState.Connected ||
        this.hub.state === signalR.HubConnectionState.Connecting) return;
    try {
      await this.hub.start();
    } catch (err) {
      console.error('[PipelineHub] Connection failed:', err);
      throw err;
    }
  }

  async joinPipeline(pipelineId: string): Promise<void> {
    await this.hub.invoke('JoinPipeline', pipelineId);
  }

  async leavePipeline(pipelineId: string): Promise<void> {
    await this.hub.invoke('LeavePipeline', pipelineId);
  }

  async openDeal(dealId: string, pipelineId: string): Promise<void> {
    await this.hub.invoke('OpenDeal', dealId, pipelineId);
  }

  async closeDeal(dealId: string, pipelineId: string): Promise<void> {
    await this.hub.invoke('CloseDeal', dealId, pipelineId);
  }

  ngOnDestroy(): void {
    this.hub.stop();
  }
}
