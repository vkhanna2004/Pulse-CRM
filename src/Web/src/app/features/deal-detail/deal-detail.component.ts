import { Component, OnInit, OnDestroy, signal, inject, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe, CurrencyPipe, TitleCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DealsService } from '../../core/api/deals.service';
import { PipelineStore } from '../../store/pipeline.store';
import { PipelineHubService } from '../../core/signalr/pipeline-hub.service';
import { AiSummaryComponent } from './ai-summary.component';

// Matching seed data in DatabaseSeeder.cs
const KNOWN_USERS = [
  { id: 'aaaaaaaa-0000-0000-0000-000000000001', name: 'Alice Demo' },
  { id: 'bbbbbbbb-0000-0000-0000-000000000002', name: 'Bob Demo' },
];

@Component({
  selector: 'app-deal-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AiSummaryComponent, DatePipe, CurrencyPipe, TitleCasePipe, RouterLink, FormsModule],
  template: `
    @if (loading()) {
      <div class="loading-state">
        <div class="spinner"></div>
        <p>Loading deal...</p>
      </div>
    } @else if (deal(); as d) {
      <div class="detail-wrap">

        <!-- Header -->
        <div class="detail-header">
          <div class="breadcrumb">
            <a routerLink="/pipeline">Pipeline</a>
            <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><polyline points="9 18 15 12 9 6"/></svg>
            <span>{{ d.title }}</span>
          </div>
          <div class="header-actions">
            @if (!editing()) {
              <button class="btn btn-ghost" (click)="startEdit(d)">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                Edit
              </button>
            } @else {
              <button class="btn btn-ghost" (click)="cancelEdit()">Cancel</button>
              <button class="btn btn-solid" (click)="saveEdit(d.id)" [disabled]="saving()">
                @if (saving()) { Saving… } @else { Save Changes }
              </button>
            }
            <button class="btn btn-ghost" (click)="recalculateScore(d.id)">Recalculate Score</button>
          </div>
        </div>

        @if (saveError()) {
          <div class="form-error">{{ saveError() }}</div>
        }

        <!-- Title (editable) -->
        <div class="title-row">
          @if (editing()) {
            <input
              class="title-input"
              [(ngModel)]="editTitle"
              placeholder="Deal title"
              maxlength="500"
            />
          } @else {
            <h1 class="deal-title">{{ d.title }}</h1>
          }
          @if (d.isClosed && !editing()) {
            <span class="closed-chip">Closed</span>
          }
        </div>

        <!-- Meta grid -->
        <div class="meta-grid">
          <!-- Value -->
          <div class="meta-item" [class.editing]="editing()">
            <span class="meta-label">Value</span>
            @if (editing()) {
              <div class="inline-input-wrap">
                <span class="inline-prefix">$</span>
                <input class="meta-input prefixed" type="number" [(ngModel)]="editValue" min="0" />
              </div>
            } @else {
              <span class="meta-value">{{ d.value | currency:d.currency:'symbol':'1.0-0' }}</span>
            }
          </div>

          <!-- Stage (read-only always) -->
          <div class="meta-item">
            <span class="meta-label">Stage</span>
            <span class="meta-value">{{ stageName(d.stageId) }}</span>
          </div>

          <!-- Score -->
          <div class="meta-item">
            <span class="meta-label">Score</span>
            <span class="meta-value score-val" [style.color]="scoreColor(d.score)">{{ d.score }}/100</span>
          </div>

          <!-- Owner -->
          <div class="meta-item" [class.editing]="editing()">
            <span class="meta-label">Owner</span>
            @if (editing()) {
              <select class="meta-select" [(ngModel)]="editOwnerId">
                @for (u of users; track u.id) {
                  <option [value]="u.id">{{ u.name }}</option>
                }
              </select>
            } @else {
              <span class="meta-value">{{ d.ownerDisplayName }}</span>
            }
          </div>

          <!-- Contact -->
          <div class="meta-item" [class.editing]="editing()" style="position:relative;">
            <span class="meta-label">Contact</span>
            @if (editing()) {
              <input
                class="meta-input"
                [(ngModel)]="editContactSearch"
                (ngModelChange)="onContactSearch($event)"
                (focus)="onContactSearch(editContactSearch)"
                placeholder="Search name / email…"
                autocomplete="off"
              />
              @if (contactResults().length > 0) {
                <div class="autocomplete-dropdown">
                  @for (c of contactResults(); track c.id) {
                    <button class="autocomplete-item" (mousedown)="selectContact(c)">
                      <span class="contact-name-ac">{{ c.name }}</span>
                      @if (c.email) { <span class="contact-email-ac">{{ c.email }}</span> }
                    </button>
                  }
                </div>
              }
              @if (editContactId()) {
                <div class="contact-chip">
                  {{ editContactName() }}
                  <button class="chip-remove" (click)="clearContact()">×</button>
                </div>
              }
            } @else {
              <span class="meta-value">{{ d.contactDisplayName || '—' }}</span>
            }
          </div>

          <!-- Close Date -->
          <div class="meta-item" [class.editing]="editing()">
            <span class="meta-label">Close Date</span>
            @if (editing()) {
              <input class="meta-input" type="date" [(ngModel)]="editCloseDate" />
            } @else {
              <span class="meta-value" [class.overdue]="isOverdue(d.expectedCloseDate)">
                {{ d.expectedCloseDate ? (d.expectedCloseDate | date:'mediumDate') : '—' }}
              </span>
            }
          </div>

          <!-- Created -->
          <div class="meta-item">
            <span class="meta-label">Created</span>
            <span class="meta-value">{{ d.createdAt | date:'mediumDate' }}</span>
          </div>
        </div>

        <!-- AI Summary — @defer defers the JS chunk until viewport/interaction -->
        @defer (on viewport; prefetch on idle) {
          <app-ai-summary [dealId]="d.id" />
        } @placeholder {
          <div class="summary-skeleton">
            <div class="skeleton-header">
              <span class="skeleton-title">AI Summary</span>
              <div class="skeleton-bar short"></div>
            </div>
            <div class="skeleton-body">
              <div class="skeleton-bar"></div>
              <div class="skeleton-bar medium"></div>
              <div class="skeleton-bar short"></div>
            </div>
          </div>
        } @loading (minimum 300ms) {
          <div class="summary-skeleton loading">
            <div class="summary-loading-spinner"></div>
            <span>Loading AI summary…</span>
          </div>
        }

        <!-- Activity log -->
        <div class="section-header">
          <span class="section-title">Activity Timeline</span>
          <div class="activity-actions">
            <button class="btn btn-ghost act-btn" (click)="toggleActivityForm('call')">
              <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.49 12a19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 3.4 1.08h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L7.91 8.97a16 16 0 0 0 6.07 6.07l1.25-1.25a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 16.92z"/></svg>
              Log Call
            </button>
            <button class="btn btn-ghost act-btn" (click)="toggleActivityForm('email')">
              <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg>
              Log Email
            </button>
            <button class="btn btn-ghost" (click)="toggleActivityForm('note')">+ Add Note</button>
          </div>
        </div>

        @if (activeForm()) {
          <div class="activity-form">
            <div class="form-type-label">
              @if (activeForm() === 'call') { 📞 Logging a Call }
              @else if (activeForm() === 'email') { ✉️ Logging an Email }
              @else { 📝 Adding a Note }
            </div>
            <textarea
              class="note-textarea"
              [(ngModel)]="activityContent"
              [placeholder]="activityPlaceholder()"
              rows="3"
            ></textarea>
            <div class="note-actions">
              <button class="btn btn-ghost" (click)="cancelActivity()">Cancel</button>
              <button
                class="btn btn-solid"
                (click)="submitActivity(d.id)"
                [disabled]="!activityContent.trim() || submittingActivity()"
              >
                @if (submittingActivity()) { Saving… }
                @else if (activeForm() === 'note') { Add Note }
                @else { Log {{ activeForm() | titlecase }}
                }
              </button>
            </div>
          </div>
        }

        <div class="activity-list">
          @for (a of activities(); track a.id) {
            <div class="activity-item">
              <div class="activity-icon">
                <span class="activity-badge" [class]="'badge-' + a.type.toLowerCase()">{{ a.type }}</span>
              </div>
              <div class="activity-body">
                <div class="activity-actor">{{ a.actorDisplayName }}</div>
                <div class="activity-content">{{ a.content }}</div>
                <div class="activity-time">{{ a.createdAt | date:'medium' }}</div>
              </div>
            </div>
          } @empty {
            <div class="empty-timeline">No activity yet. Add a note or log a call to get started.</div>
          }
        </div>

      </div>
    } @else {
      <div class="error-state">
        <p>Deal not found.</p>
        <a routerLink="/pipeline" class="btn btn-ghost">Back to Pipeline</a>
      </div>
    }
  `,
  styles: [`
    :host { display: block; background: var(--bg); height: 100%; overflow-y: auto; }

    .loading-state, .error-state {
      display: flex; flex-direction: column;
      align-items: center; justify-content: center;
      min-height: 60vh; gap: 16px;
      color: var(--text-3);
      font-size: .88rem;
    }
    .spinner {
      width: 28px; height: 28px;
      border: 2px solid var(--border);
      border-top-color: var(--accent);
      border-radius: 50%;
      animation: spin .8s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }

    .detail-wrap {
      max-width: 820px;
      margin: 0 auto;
      padding: 24px 24px 56px;
    }

    .detail-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 16px;
      gap: 12px;
      flex-wrap: wrap;
    }
    .breadcrumb {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: .78rem;
      color: var(--text-3);
    }
    .breadcrumb a { color: var(--text-2); }
    .breadcrumb a:hover { color: var(--text); }
    .header-actions { display: flex; gap: 8px; align-items: center; }

    .form-error {
      background: var(--red-dim);
      border: 1px solid rgba(248,113,113,0.3);
      color: var(--red);
      border-radius: 6px;
      padding: 9px 12px;
      font-size: .8rem;
      margin-bottom: 12px;
    }

    .title-row {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }
    .deal-title {
      font-size: 1.3rem;
      font-weight: 700;
      color: var(--text);
      letter-spacing: -.03em;
      line-height: 1.25;
    }
    .title-input {
      flex: 1;
      font-size: 1.1rem;
      font-weight: 700;
      color: var(--text);
      letter-spacing: -.02em;
      background: var(--bg-2);
      border: 1px solid var(--accent);
      border-radius: 6px;
      padding: 7px 12px;
      font-family: var(--font);
      outline: none;
      box-shadow: 0 0 0 3px var(--accent-dim);
    }
    .closed-chip {
      font-size: .72rem;
      font-weight: 600;
      background: var(--bg-3);
      border: 1px solid var(--border);
      color: var(--text-3);
      padding: 2px 10px;
      border-radius: 10px;
    }

    /* Meta grid */
    .meta-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1px;
      background: var(--border);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      overflow: visible;
      margin-bottom: 20px;
    }
    .meta-item {
      background: var(--bg-3);
      padding: 12px 14px;
      display: flex;
      flex-direction: column;
      gap: 4px;
      transition: background 200ms ease;
      position: relative;
    }
    .meta-item.editing { background: var(--bg-2); }
    .meta-label {
      font-size: .68rem;
      text-transform: uppercase;
      letter-spacing: .08em;
      color: var(--text-3);
      font-weight: 600;
    }
    .meta-value { font-size: .88rem; color: var(--text); font-weight: 500; }
    .score-val { font-weight: 700; }
    .overdue { color: var(--red) !important; }

    /* Inline edit inputs inside meta grid */
    .meta-input, .meta-select {
      width: 100%;
      padding: 5px 8px;
      background: var(--bg);
      border: 1px solid var(--border-md);
      border-radius: 5px;
      font-family: var(--font);
      font-size: .82rem;
      color: var(--text);
      outline: none;
      transition: border-color 150ms;
    }
    .meta-input:focus, .meta-select:focus { border-color: var(--accent); }
    .meta-select { cursor: pointer; appearance: none; }
    .inline-input-wrap { position: relative; }
    .inline-prefix { position: absolute; left: 8px; top: 50%; transform: translateY(-50%); color: var(--text-3); font-size: .82rem; pointer-events: none; }
    .meta-input.prefixed { padding-left: 18px; }

    /* Contact autocomplete in meta */
    .autocomplete-dropdown {
      position: absolute;
      top: calc(100% - 4px);
      left: 0; right: 0;
      background: var(--bg-3);
      border: 1px solid var(--border-md);
      border-radius: var(--radius);
      box-shadow: var(--shadow);
      z-index: 50;
      overflow: hidden;
    }
    .autocomplete-item {
      width: 100%;
      padding: 8px 12px;
      text-align: left;
      background: none;
      border: none;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      gap: 2px;
      transition: background 120ms;
    }
    .autocomplete-item:hover { background: var(--bg-hover); }
    .contact-name-ac { font-size: .8rem; color: var(--text); font-weight: 500; }
    .contact-email-ac { font-size: .7rem; color: var(--text-3); }
    .contact-chip {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      background: var(--accent-dim);
      border: 1px solid rgba(99,102,241,0.3);
      color: #a5b4fc;
      border-radius: 20px;
      padding: 2px 8px;
      font-size: .73rem;
      margin-top: 4px;
    }
    .chip-remove { background: none; border: none; color: inherit; cursor: pointer; font-size: .95rem; padding: 0; opacity: 0.7; }
    .chip-remove:hover { opacity: 1; }

    /* Timeline */
    .section-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;
      gap: 8px;
      flex-wrap: wrap;
    }
    .section-title {
      font-size: .75rem;
      font-weight: 600;
      color: var(--text-3);
      text-transform: uppercase;
      letter-spacing: .07em;
    }
    .activity-actions { display: flex; gap: 6px; }
    .act-btn { display: flex; align-items: center; gap: 5px; }

    .activity-form {
      background: var(--bg-3);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      padding: 12px;
      margin-bottom: 14px;
      animation: fadeIn 150ms ease;
    }
    @keyframes fadeIn { from { opacity: 0; transform: translateY(-4px); } to { opacity: 1; transform: translateY(0); } }
    .form-type-label {
      font-size: .75rem;
      font-weight: 600;
      color: var(--text-3);
      margin-bottom: 8px;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .note-textarea {
      width: 100%;
      padding: 9px 11px;
      background: var(--bg);
      border: 1px solid var(--border-md);
      border-radius: 6px;
      font-family: var(--font, inherit);
      font-size: .82rem;
      color: var(--text);
      resize: vertical;
      margin-bottom: 10px;
      min-height: 76px;
      outline: none;
      line-height: 1.5;
      transition: border-color 150ms;
    }
    .note-textarea::placeholder { color: var(--text-4); }
    .note-textarea:focus { border-color: var(--accent); }
    .note-actions { display: flex; gap: 8px; justify-content: flex-end; }

    .activity-list { display: flex; flex-direction: column; }
    .activity-item {
      display: flex;
      gap: 12px;
      padding: 12px 0;
      border-bottom: 1px solid var(--border);
    }
    .activity-item:last-child { border-bottom: none; }
    .activity-icon { flex-shrink: 0; padding-top: 1px; }
    .activity-badge {
      font-size: .68rem;
      font-weight: 600;
      padding: 2px 8px;
      border-radius: 4px;
      display: inline-block;
      white-space: nowrap;
      text-transform: capitalize;
    }
    .badge-note        { background: var(--accent-dim); color: #a5b4fc; }
    .badge-call        { background: var(--green-dim);  color: var(--green); }
    .badge-email       { background: var(--amber-dim);  color: var(--amber); }
    .badge-stagechange { background: rgba(139,92,246,.12); color: #c4b5fd; }
    .badge-assignment  { background: rgba(14,165,233,.12); color: #7dd3fc; }

    .activity-body { flex: 1; min-width: 0; }
    .activity-actor { font-weight: 600; font-size: .78rem; color: var(--text-2); margin-bottom: 3px; }
    .activity-content { color: var(--text-2); font-size: .83rem; line-height: 1.5; margin-bottom: 3px; }
    .activity-time { font-size: .7rem; color: var(--text-4); }

    .empty-timeline {
      padding: 32px;
      text-align: center;
      color: var(--text-4);
      font-style: italic;
      font-size: .85rem;
    }

    /* @defer placeholder skeleton for AI summary */
    .summary-skeleton {
      background: var(--bg-3);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      overflow: hidden;
      margin-bottom: 20px;
    }
    .skeleton-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 11px 14px;
      border-bottom: 1px solid var(--border);
      background: var(--bg-4);
    }
    .skeleton-title { font-size: .82rem; font-weight: 600; color: var(--text-3); }
    .skeleton-body { padding: 14px; display: flex; flex-direction: column; gap: 8px; }
    .skeleton-bar {
      height: 10px;
      border-radius: 4px;
      background: var(--bg-4);
      animation: shimmer 1.4s ease infinite;
      width: 100%;
    }
    .skeleton-bar.medium { width: 80%; }
    .skeleton-bar.short { width: 50%; height: 16px; border-radius: 5px; }
    @keyframes shimmer {
      0%,100% { opacity: 0.4; }
      50% { opacity: 0.8; }
    }
    .summary-skeleton.loading {
      display: flex; align-items: center; gap: 12px;
      padding: 16px; color: var(--text-3); font-size: .82rem;
    }
    .summary-loading-spinner {
      width: 16px; height: 16px;
      border: 2px solid var(--border);
      border-top-color: var(--accent);
      border-radius: 50%;
      animation: spin .8s linear infinite;
      flex-shrink: 0;
    }
  `]
})
export class DealDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private dealsService = inject(DealsService);
  private pipelineStore = inject(PipelineStore);
  private hub = inject(PipelineHubService);

  deal = signal<any>(null);
  activities = signal<any[]>([]);
  loading = signal(true);

  // Edit mode
  editing = signal(false);
  saving = signal(false);
  saveError = signal<string | null>(null);
  editTitle = '';
  editValue = 0;
  editOwnerId = '';
  editContactSearch = '';
  editContactId = signal<string | null>(null);
  editContactName = signal<string>('');
  editCloseDate = '';
  contactResults = signal<any[]>([]);
  users = KNOWN_USERS;

  // Activity form
  activeForm = signal<'note' | 'call' | 'email' | null>(null);
  activityContent = '';
  submittingActivity = signal(false);

  private _contactTimer: any;

  activityPlaceholder(): string {
    switch (this.activeForm()) {
      case 'call': return 'Describe the call — outcome, next steps, objections…';
      case 'email': return 'Summarize the email thread — what was discussed, any action items…';
      default: return 'Add a note… use @alice or @bob to mention teammates';
    }
  }

  async ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    try {
      const data = await this.dealsService.getDeal(id);
      this.deal.set(data.deal);
      this.activities.set(data.activities ?? []);
      await this.hub.connect();
      await this.hub.openDeal(id, data.deal.stageId);
    } catch { } finally { this.loading.set(false); }
  }

  async ngOnDestroy() {
    const d = this.deal();
    if (d) { try { await this.hub.closeDeal(d.id, d.stageId); } catch {} }
  }

  scoreColor(score: number): string {
    if (score >= 70) return 'var(--green)';
    if (score >= 40) return 'var(--amber)';
    return 'var(--red)';
  }

  stageName(stageId: string): string {
    const stages = this.pipelineStore.pipeline()?.stages;
    return stages?.find(s => s.id === stageId)?.name ?? stageId;
  }

  isOverdue(date?: string): boolean {
    if (!date) return false;
    return new Date(date) < new Date();
  }

  // ─── Edit mode ──────────────────────────────────────────────
  startEdit(d: any): void {
    this.editTitle = d.title ?? '';
    this.editValue = d.value ?? 0;
    this.editOwnerId = d.ownerId ?? KNOWN_USERS[0].id;
    this.editContactId.set(d.contactId ?? null);
    this.editContactName.set(d.contactDisplayName ?? '');
    this.editContactSearch = '';
    this.editCloseDate = d.expectedCloseDate
      ? new Date(d.expectedCloseDate).toISOString().split('T')[0]
      : '';
    this.saveError.set(null);
    this.editing.set(true);
  }

  cancelEdit(): void {
    this.editing.set(false);
    this.saveError.set(null);
    this.contactResults.set([]);
  }

  async saveEdit(dealId: string): Promise<void> {
    const trimmed = this.editTitle.trim();
    if (!trimmed) return;
    this.saving.set(true);
    this.saveError.set(null);
    try {
      const body = {
        title: trimmed,
        value: this.editValue,
        ownerId: this.editOwnerId,
        contactId: this.editContactId() ?? null,
        expectedCloseDate: this.editCloseDate || null,
      };
      const updated = await this.dealsService.updateDeal(dealId, body);
      this.deal.set(updated);
      this.editing.set(false);
    } catch (err: any) {
      this.saveError.set(err?.error?.title ?? 'Failed to save changes.');
    } finally {
      this.saving.set(false);
    }
  }

  // ─── Contact autocomplete in edit mode ──────────────────────
  onContactSearch(val: string): void {
    clearTimeout(this._contactTimer);
    if (!val.trim() || this.editContactId()) { this.contactResults.set([]); return; }
    this._contactTimer = setTimeout(async () => {
      try {
        const results = await this.dealsService.searchContacts(val.trim());
        this.contactResults.set(results.slice(0, 6));
      } catch { this.contactResults.set([]); }
    }, 250);
  }

  selectContact(c: any): void {
    this.editContactId.set(c.id);
    this.editContactName.set(c.name);
    this.editContactSearch = '';
    this.contactResults.set([]);
  }

  clearContact(): void {
    this.editContactId.set(null);
    this.editContactName.set('');
    this.editContactSearch = '';
  }

  // ─── Activity form ───────────────────────────────────────────
  toggleActivityForm(type: 'note' | 'call' | 'email'): void {
    if (this.activeForm() === type) {
      this.cancelActivity();
    } else {
      this.activityContent = '';
      this.activeForm.set(type);
    }
  }

  cancelActivity(): void {
    this.activeForm.set(null);
    this.activityContent = '';
  }

  async submitActivity(dealId: string): Promise<void> {
    const content = this.activityContent.trim();
    const type = this.activeForm();
    if (!content || !type) return;

    this.submittingActivity.set(true);
    try {
      let activity: any;
      if (type === 'note') {
        activity = await this.dealsService.addNote(dealId, content);
      } else {
        activity = await this.dealsService.logActivity(dealId, type, content);
      }
      this.activities.update(list => [activity, ...list]);
      this.cancelActivity();
    } catch { } finally {
      this.submittingActivity.set(false);
    }
  }

  // ─── Score recalculation ─────────────────────────────────────
  async recalculateScore(dealId: string): Promise<void> {
    try {
      const res = await this.dealsService.recalculateScore(dealId);
      this.deal.update(d => ({ ...d, score: res.score }));
    } catch {}
  }
}
