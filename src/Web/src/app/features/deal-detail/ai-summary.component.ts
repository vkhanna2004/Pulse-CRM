import { Component, ChangeDetectionStrategy, OnInit, OnDestroy, signal, inject, effect, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { DealsService } from '../../core/api/deals.service';
import { AiHubService } from '../../core/signalr/ai-hub.service';

@Component({
  selector: 'app-ai-summary',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  template: `
    <div class="summary-card">
      <div class="summary-header">
        <div class="summary-title">
          AI Summary
          <span class="model-badge">Gemini 2.5</span>
        </div>
        <button class="btn btn-accent" (click)="regenerate()" [disabled]="streaming()">
          @if (streaming()) {
            <span class="spinner-xs"></span>Generating
          } @else {
            Summarize
          }
        </button>
      </div>

      <div class="summary-body">
        @if (error()) {
          <p class="summary-error">{{ error() }}</p>
        } @else if (summary()) {
          <p class="summary-text">{{ summary() }}</p>
          @if (generatedAt()) {
            <div class="summary-meta">Generated {{ generatedAt() | date:'medium' }}</div>
          }
        } @else if (streaming()) {
          <p class="summary-text streaming">Analyzing timeline<span class="cursor-blink"></span></p>
        } @else {
          <p class="summary-empty">Click "Summarize" to generate an AI summary of this deal's timeline.</p>
        }
      </div>
    </div>
  `,
  styles: [`
    .summary-card {
      background: var(--bg-3);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      overflow: hidden;
      margin-bottom: 20px;
    }
    .summary-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 11px 14px;
      border-bottom: 1px solid var(--border);
      background: var(--bg-4);
    }
    .summary-title {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: .82rem;
      font-weight: 600;
      color: var(--text);
    }
    .model-badge {
      font-size: .63rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: .05em;
      background: linear-gradient(135deg, rgba(99,102,241,0.18), rgba(139,92,246,0.18));
      border: 1px solid rgba(99,102,241,0.25);
      color: #a5b4fc;
      padding: 2px 7px;
      border-radius: 4px;
    }
    .summary-body { padding: 14px; }
    .summary-text {
      font-size: .85rem;
      line-height: 1.75;
      color: var(--text-2);
    }
    .summary-text.streaming { color: var(--text-3); font-style: italic; }
    .summary-meta { margin-top: 10px; font-size: .7rem; color: var(--text-4); }
    .summary-empty { color: var(--text-3); font-size: .82rem; font-style: italic; }
    .summary-error { color: var(--red); font-size: .82rem; }

    .cursor-blink {
      display: inline-block;
      width: 2px; height: 13px;
      background: var(--accent);
      margin-left: 2px;
      vertical-align: text-bottom;
      border-radius: 1px;
      animation: blink .9s step-start infinite;
      box-shadow: 0 0 6px var(--accent-glow);
    }
    @keyframes blink { 0%,100%{opacity:1} 50%{opacity:0} }

    .spinner-xs {
      width: 11px; height: 11px;
      border: 1.5px solid rgba(165,180,252,0.3);
      border-top-color: #a5b4fc;
      border-radius: 50%;
      animation: spin .7s linear infinite;
      display: inline-block;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class AiSummaryComponent implements OnInit, OnDestroy {
  // Angular 20: signal input — replaces @Input() decorator
  dealId = input.required<string>();

  private dealsService = inject(DealsService);
  private aiHub = inject(AiHubService);

  summary = signal('');
  generatedAt = signal<string | null>(null);
  streaming = signal(false);
  error = signal('');
  private buffer = '';

  constructor() {
    effect(() => {
      const ev = this.aiHub.summaryStreaming();
      if (ev && ev.dealId === this.dealId()) {
        this.streaming.set(true); this.error.set('');
        this.buffer += ev.chunk || '';
        this.summary.set(this.buffer);
      }
    });
    effect(() => {
      const ev = this.aiHub.summaryCompleted();
      if (ev && ev.dealId === this.dealId()) {
        this.summary.set(ev.summary);
        this.generatedAt.set(ev.generatedAt);
        this.streaming.set(false);
        this.buffer = '';
      }
    });
    effect(() => {
      const ev = this.aiHub.summaryFailed();
      if (ev && ev.dealId === this.dealId()) {
        this.error.set(ev.reason ?? 'Generation failed');
        this.streaming.set(false); this.buffer = '';
      }
    });
  }

  async ngOnInit() {
    try {
      await this.aiHub.connect();
      await this.aiHub.watchDeal(this.dealId());
      const existing = await this.dealsService.getSummary(this.dealId());
      if (existing?.summary) { this.summary.set(existing.summary); this.generatedAt.set(existing.generatedAt ?? null); }
    } catch {}
  }

  async ngOnDestroy() { try { await this.aiHub.unwatchDeal(this.dealId()); } catch {} }

  async regenerate() {
    this.buffer = ''; this.summary.set(''); this.error.set(''); this.streaming.set(true);
    try { await this.dealsService.regenerateSummary(this.dealId()); }
    catch { this.error.set('Failed to start. Please try again.'); this.streaming.set(false); }
  }
}
