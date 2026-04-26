import {
  Component, ChangeDetectionStrategy, computed,
  input, output
} from '@angular/core';
import {
  CdkDropList, CdkDrag, CdkDragDrop, CdkDragPlaceholder
} from '@angular/cdk/drag-drop';
import { Deal, Stage } from '../../store/pipeline.store';
import { DealCardComponent } from './deal-card.component';

const STAGE_COLORS: Record<string, string> = {
  Lead: '#666',
  Qualified: '#f59e0b',
  Proposal: '#6366f1',
  Negotiation: '#3ecf8e',
  Won: '#3ecf8e',
  Lost: '#f87171',
};

@Component({
  selector: 'app-kanban-column',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CdkDropList, CdkDrag, CdkDragPlaceholder, DealCardComponent],
  template: `
    <div class="column">
      <div class="column-header">
        <div class="column-name-row">
          <span class="stage-dot"
            [style.background]="stageColor()"
            [style.box-shadow]="stage().isTerminal ? '0 0 6px ' + stageColor() : 'none'"
          ></span>
          <span class="column-name">{{ stage().name }}</span>
        </div>
        <div class="header-right">
          <span class="column-count">{{ stage().deals.length }}</span>
          @if (!stage().isTerminal) {
            <button class="add-btn" (click)="addDealClicked.emit(stage().id)" [title]="'Add deal to ' + stage().name">
              <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            </button>
          }
        </div>
      </div>

      <div cdkDropList
           [cdkDropListData]="stage().deals"
           [id]="stage().id"
           [cdkDropListConnectedTo]="connectedTo()"
           (cdkDropListDropped)="onDrop($event)"
           class="drop-zone">
        @for (deal of stage().deals; track deal.id) {
          <div cdkDrag [cdkDragData]="deal"
               (cdkDragStarted)="onDragStarted()"
               (cdkDragEnded)="onDragEnded()">
            <app-deal-card [deal]="deal" (cardClicked)="onCardClick($event)" />
            <div *cdkDragPlaceholder class="drag-placeholder"></div>
          </div>
        }
        @if (stage().deals.length === 0) {
          <div class="empty-zone">No deals</div>
        }
      </div>
    </div>
  `,
  styles: [`
    .column {
      min-width: 258px;
      max-width: 258px;
      display: flex;
      flex-direction: column;
      gap: 8px;
      flex-shrink: 0;
    }
    .column-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 4px 2px;
    }
    .column-name-row { display: flex; align-items: center; gap: 7px; }
    .stage-dot {
      width: 7px; height: 7px;
      border-radius: 50%;
      flex-shrink: 0;
      transition: box-shadow 200ms ease;
    }
    .column-name {
      font-size: .72rem;
      font-weight: 600;
      color: var(--text-3);
      text-transform: uppercase;
      letter-spacing: .07em;
    }
    .header-right { display: flex; align-items: center; gap: 5px; }
    .column-count {
      font-size: .7rem;
      color: var(--text-4);
      font-weight: 500;
      background: var(--bg-3);
      border: 1px solid var(--border);
      border-radius: 4px;
      padding: 0 6px;
      height: 18px;
      display: flex;
      align-items: center;
    }
    .add-btn {
      width: 20px; height: 20px;
      background: none;
      border: 1px solid var(--border);
      border-radius: 4px;
      color: var(--text-3);
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      transition: background 150ms, border-color 150ms, color 150ms;
      flex-shrink: 0;
      padding: 0;
    }
    .add-btn:hover {
      background: var(--accent-dim);
      border-color: rgba(99,102,241,0.4);
      color: #a5b4fc;
    }
    .drop-zone { display: flex; flex-direction: column; gap: 6px; min-height: 64px; }
    .empty-zone {
      border: 1px dashed var(--border);
      border-radius: var(--radius);
      height: 64px;
      display: flex;
      align-items: center;
      justify-content: center;
      color: var(--text-4);
      font-size: .76rem;
    }
    .drag-placeholder {
      height: 68px;
      border: 1px dashed var(--border-md);
      border-radius: var(--radius);
      background: var(--accent-dim);
    }
  `]
})
export class KanbanColumnComponent {
  // Angular 20: signal inputs — type-safe, no decorator boilerplate
  stage = input.required<Stage>();
  connectedTo = input<string[]>([]);

  // Angular 20: signal outputs
  dealDropped = output<{ deal: Deal; fromStageId: string; toStageId: string; positionInStage: number }>();
  addDealClicked = output<string>();
  dealCardClicked = output<string>();

  // computed() signal — only recalculates when stage().name changes
  stageColor = computed(() => STAGE_COLORS[this.stage().name] ?? '#666');

  private _isDragging = false;

  onDragStarted(): void {
    this._isDragging = true;
  }

  onDragEnded(): void {
    // Brief timeout so the subsequent (click) event after dragend is suppressed
    setTimeout(() => { this._isDragging = false; }, 50);
  }

  onCardClick(dealId: string): void {
    if (!this._isDragging) {
      this.dealCardClicked.emit(dealId);
    }
  }

  onDrop(event: CdkDragDrop<Deal[]>): void {
    const deal: Deal = event.item.data;
    const isSame = event.previousContainer === event.container;
    const targetDeals = [...event.container.data];
    const forCalc = isSame ? targetDeals.filter(d => d.id !== deal.id) : targetDeals;
    const newPos = computePosition(forCalc, event.currentIndex);
    this.dealDropped.emit({ deal, fromStageId: deal.stageId, toStageId: this.stage().id, positionInStage: newPos });
  }
}

function computePosition(deals: Deal[], index: number): number {
  if (deals.length === 0) return 1000;
  const prev = index > 0 ? deals[index - 1].positionInStage : 0;
  const next = index < deals.length ? deals[index].positionInStage : prev + 2000;
  return Math.floor((prev + next) / 2);
}
