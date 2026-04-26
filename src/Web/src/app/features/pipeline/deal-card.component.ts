import {
  Component, ChangeDetectionStrategy, OnChanges, SimpleChanges,
  input, output
} from '@angular/core';
import { Deal } from '../../store/pipeline.store';
import { DecimalPipe, DatePipe } from '@angular/common';

@Component({
  selector: 'app-deal-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, DatePipe],
  template: `
    <div
      class="deal-card"
      [class.closed]="deal().isClosed"
      (click)="onClick($event)"
    >
      <div class="card-top">
        <span class="deal-title">{{ deal().title }}</span>
        <span class="score-badge" [style]="_scoreBadgeStyle">{{ deal().score }}</span>
      </div>
      <div class="card-meta">
        <span class="deal-value">\${{ deal().value | number }}</span>
        <div class="deal-owner">
          <div class="owner-dot" [style]="_ownerStyle">{{ _ownerInitials }}</div>
          <span>{{ _ownerFirst }}</span>
        </div>
      </div>
      @if (deal().contactDisplayName || deal().expectedCloseDate) {
        <div class="card-footer">
          @if (deal().contactDisplayName) {
            <span class="contact-name">
              <svg width="9" height="9" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/></svg>
              {{ deal().contactDisplayName }}
            </span>
          }
          @if (deal().expectedCloseDate) {
            <span class="close-date" [class.overdue]="_isOverdue">
              <svg width="9" height="9" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
              {{ deal().expectedCloseDate | date:'MMM d' }}
            </span>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .deal-card {
      background: var(--bg-3);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      padding: 11px 12px;
      cursor: pointer;
      display: block;
      transition: background 150ms ease, border-color 150ms ease, transform 100ms ease;
      position: relative;
      overflow: hidden;
      user-select: none;
    }
    .deal-card::before {
      content: '';
      position: absolute;
      inset: 0;
      background: linear-gradient(135deg, rgba(255,255,255,0.025) 0%, transparent 60%);
      pointer-events: none;
    }
    .deal-card:hover {
      background: var(--bg-4);
      border-color: var(--border-hl);
      transform: translateY(-1px);
    }
    .deal-card.closed { opacity: 0.45; }
    .card-top {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 8px;
      margin-bottom: 9px;
    }
    .deal-title {
      font-size: .82rem;
      font-weight: 500;
      color: var(--text);
      line-height: 1.35;
      flex: 1;
    }
    .score-badge {
      font-size: .67rem;
      font-weight: 700;
      padding: 2px 7px;
      border-radius: 5px;
      white-space: nowrap;
      flex-shrink: 0;
    }
    .card-meta {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .deal-value {
      font-size: .78rem;
      color: var(--text-2);
      font-weight: 500;
      font-variant-numeric: tabular-nums;
    }
    .deal-owner {
      display: flex;
      align-items: center;
      gap: 5px;
      font-size: .72rem;
      color: var(--text-3);
    }
    .owner-dot {
      width: 18px; height: 18px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: .55rem;
      font-weight: 700;
      color: white;
      flex-shrink: 0;
    }
    .card-footer {
      margin-top: 8px;
      padding-top: 8px;
      border-top: 1px solid var(--border);
      display: flex;
      align-items: center;
      gap: 10px;
      flex-wrap: wrap;
    }
    .contact-name, .close-date {
      font-size: .72rem;
      color: var(--text-3);
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .close-date.overdue { color: var(--red); }
    .close-date.overdue svg { stroke: var(--red); }
  `]
})
export class DealCardComponent implements OnChanges {
  // Angular 20: signal-based input — no @Input() decorator
  deal = input.required<Deal>();
  // Angular 20: signal-based output — no @Output() EventEmitter
  cardClicked = output<string>();

  // Pre-computed on input change only — never recalculated during CD
  _scoreBadgeStyle = '';
  _ownerStyle = '';
  _ownerInitials = '';
  _ownerFirst = '';
  _isOverdue = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['deal']) {
      this._recompute();
    }
  }

  private _recompute(): void {
    const d = this.deal();
    const s = d.score;
    this._scoreBadgeStyle = s >= 70
      ? 'background:var(--green-dim);color:var(--green)'
      : s >= 40
        ? 'background:var(--amber-dim);color:var(--amber)'
        : 'background:var(--red-dim);color:var(--red)';

    const name = d.ownerDisplayName ?? '';
    this._ownerStyle = name.toLowerCase().startsWith('b')
      ? 'background:linear-gradient(135deg,#0ea5e9,#6366f1)'
      : 'background:linear-gradient(135deg,#6366f1,#8b5cf6)';

    this._ownerInitials = name.split(' ').map(w => w[0]).join('').slice(0, 2).toUpperCase();
    this._ownerFirst = name.split(' ')[0];
    this._isOverdue = !!d.expectedCloseDate && new Date(d.expectedCloseDate) < new Date();
  }

  onClick(_event: MouseEvent): void {
    this.cardClicked.emit(this.deal().id);
  }
}
