import { Component, OnInit, inject, effect, computed, signal, ChangeDetectionStrategy, untracked } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { PipelineStore, Deal } from '../../store/pipeline.store';
import { DealsService } from '../../core/api/deals.service';
import { PipelineHubService } from '../../core/signalr/pipeline-hub.service';
import { KanbanColumnComponent } from './kanban-column.component';
import { CreateDealModalComponent } from './create-deal-modal.component';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-pipeline',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DragDropModule, KanbanColumnComponent, CurrencyPipe, CreateDealModalComponent],
  template: `
    @if (store.loading()) {
      <div class="loading-state">
        <div class="spinner"></div>
        <p>Loading pipeline...</p>
      </div>
    } @else if (store.error(); as err) {
      <div class="error-state">
        <p>{{ err }}</p>
        <button class="btn btn-ghost" (click)="reload()">Retry</button>
      </div>
    } @else if (store.pipeline(); as pipeline) {
      <div class="page-toolbar">
        <div class="page-title">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" style="opacity:.5"><rect x="3" y="3" width="6" height="18" rx="1"/><rect x="10" y="3" width="4" height="18" rx="1"/><rect x="15" y="3" width="6" height="18" rx="1"/></svg>
          {{ pipeline.name }}
        </div>
        <div class="toolbar-right">
          <div class="pipeline-stats">
            <div class="stat"><span class="stat-label">Deals</span><span class="stat-value">{{ totalDeals() }}</span></div>
            <div class="stat"><span class="stat-label">Pipeline</span><span class="stat-value">{{ totalValue() | currency:'USD':'symbol':'1.0-0' }}</span></div>
            <div class="stat">
              <span class="online-dot"></span>
              <span class="stat-value online">{{ onlineCount }} online</span>
            </div>
          </div>
          <button id="new-deal-btn" class="btn btn-solid new-deal-btn" (click)="openModal(null)">
            <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            New Deal
          </button>
        </div>
      </div>

      <div class="board-scroll">
        @for (stage of pipeline.stages; track stage.id) {
          <app-kanban-column
            [stage]="stage"
            [connectedTo]="stageIds()"
            (dealDropped)="onDealDropped($event)"
            (addDealClicked)="openModal($event)"
            (dealCardClicked)="onCardClicked($event)"
          />
        }
      </div>
    }

    @if (showModal()) {
      <app-create-deal-modal
        [stages]="store.pipeline()?.stages ?? []"
        [defaultStageId]="modalStageId()"
        (saved)="onDealCreated($event)"
        (cancelled)="closeModal()"
      />
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; flex: 1; overflow: hidden; }

    .loading-state, .error-state {
      display: flex; flex-direction: column;
      align-items: center; justify-content: center;
      min-height: 60vh; gap: 16px;
      color: var(--text-3);
    }
    .spinner {
      width: 32px; height: 32px;
      border: 2px solid var(--border);
      border-top-color: var(--accent);
      border-radius: 50%;
      animation: spin .8s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }

    .page-toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 14px 20px;
      border-bottom: 1px solid var(--border);
      flex-shrink: 0;
    }
    .page-title {
      display: flex;
      align-items: center;
      gap: 9px;
      font-size: .9rem;
      font-weight: 600;
      color: var(--text);
      letter-spacing: -.02em;
    }
    .toolbar-right {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .pipeline-stats { display: flex; gap: 20px; }
    .stat { display: flex; align-items: center; gap: 6px; }
    .stat-label { font-size: .75rem; color: var(--text-3); }
    .stat-value { font-size: .78rem; font-weight: 500; color: var(--text-2); }
    .stat-value.online { color: var(--green); }
    .online-dot {
      width: 6px; height: 6px;
      border-radius: 50%;
      background: var(--green);
      box-shadow: 0 0 6px var(--green);
    }
    .new-deal-btn {
      font-size: .76rem;
      padding: 5px 11px;
    }

    .board-scroll {
      flex: 1;
      overflow-x: auto;
      overflow-y: hidden;
      padding: 16px 20px 20px;
      display: flex;
      gap: 12px;
      align-items: flex-start;
    }
    .board-scroll::-webkit-scrollbar { height: 4px; }
    .board-scroll::-webkit-scrollbar-thumb { background: var(--border-md); border-radius: 2px; }
  `]
})
export class PipelineComponent implements OnInit {
  store = inject(PipelineStore);
  private deals = inject(DealsService);
  private hub = inject(PipelineHubService);
  private router = inject(Router);
  onlineCount = 2;

  showModal = signal(false);
  modalStageId = signal('');

  stageIds = computed(() => this.store.pipeline()?.stages.map(s => s.id) ?? []);
  totalDeals = computed(() => this.store.pipeline()?.stages.reduce((s, st) => s + st.deals.length, 0) ?? 0);
  totalValue = computed(() => this.store.pipeline()?.stages.flatMap(s => s.deals).reduce((s, d) => s + d.value, 0) ?? 0);

  constructor() {
    effect(() => { const ev = this.hub.dealMoved(); if (ev) untracked(() => this.store.applyDealMoved(ev)); });
    effect(() => { const ev = this.hub.dealCreated(); if (ev) untracked(() => this.store.applyDealCreated(ev)); });
    effect(() => { const ev = this.hub.dealUpdated(); if (ev) untracked(() => this.store.applyDealUpdated(ev)); });
    effect(() => { const ev = this.hub.dealScoreUpdated(); if (ev) untracked(() => this.store.applyScoreUpdated(ev)); });
    effect(() => { const ev = this.hub.presenceChanged(); if (ev) untracked(() => this.store.applyPresenceChanged(ev)); });
  }

  async ngOnInit() { await this.reload(); }

  async reload() {
    this.store.setLoading(true);
    try {
      const pipeline = await this.deals.getPipeline();
      this.store.setPipeline(pipeline);
      await this.hub.connect();
      await this.hub.joinPipeline(pipeline.id);
    } catch (err) {
      this.store.setError('Failed to load pipeline.');
    }
  }

  openModal(stageId: string | null): void {
    const defaultId = stageId
      ?? this.store.pipeline()?.stages.find(s => !s.isTerminal)?.id
      ?? '';
    this.modalStageId.set(defaultId);
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
  }

  onDealCreated(deal: any): void {
    this.store.applyDealCreated(deal);
    this.closeModal();
  }

  onCardClicked(dealId: string): void {
    this.router.navigate(['/deals', dealId]);
  }

  async onDealDropped(event: { deal: Deal; fromStageId: string; toStageId: string; positionInStage: number }) {
    this.store.optimisticallyMoveDeal(event.deal.id, event.fromStageId, event.toStageId, event.positionInStage);
    try {
      const updated = await firstValueFrom(this.deals.moveDeal(event.deal.id, {
        stageId: event.toStageId,
        positionInStage: event.positionInStage,
        rowVersion: event.deal.rowVersion
      }));
      if (updated) this.store.serverApplyDealMove(updated);
    } catch (err: any) {
      const pipeline = await this.deals.getPipeline();
      this.store.setPipeline(pipeline);
      if (err.status === 409) this.store.setError('Deal updated by another user — refreshed.');
      else this.store.setError('');
      setTimeout(() => this.store.setError(null), 4000);
    }
  }
}
