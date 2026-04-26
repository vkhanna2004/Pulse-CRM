import {
  Component, ChangeDetectionStrategy, OnInit, signal, linkedSignal,
  input, output
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DealsService } from '../../core/api/deals.service';
import { Stage } from '../../store/pipeline.store';

// Seed user IDs matching DatabaseSeeder.cs and Keycloak realm
const KNOWN_USERS = [
  { id: 'aaaaaaaa-0000-0000-0000-000000000001', name: 'Alice Demo' },
  { id: 'bbbbbbbb-0000-0000-0000-000000000002', name: 'Bob Demo' },
];

@Component({
  selector: 'app-create-deal-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule],
  template: `
    <div class="modal-backdrop" (click)="onBackdropClick($event)">
      <div class="modal" role="dialog" aria-modal="true" aria-label="Create Deal">
        <div class="modal-header">
          <h2 class="modal-title">New Deal</h2>
          <button class="close-btn" (click)="cancelled.emit()" aria-label="Close">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
          </button>
        </div>

        <div class="modal-body">
          <!-- Title -->
          <div class="field">
            <label class="field-label" for="deal-title">Deal Title <span class="required">*</span></label>
            <input
              id="deal-title"
              class="field-input"
              [(ngModel)]="title"
              placeholder="e.g. Acme Corp — Q3 Expansion"
              maxlength="500"
              autocomplete="off"
            />
          </div>

          <!-- Value + Currency -->
          <div class="field-row">
            <div class="field flex-2">
              <label class="field-label" for="deal-value">Deal Value</label>
              <div class="input-prefix-wrap">
                <span class="input-prefix">$</span>
                <input id="deal-value" class="field-input prefixed" type="number" [(ngModel)]="value" placeholder="0" min="0" />
              </div>
            </div>
            <div class="field flex-1">
              <label class="field-label" for="deal-currency">Currency</label>
              <select id="deal-currency" class="field-select" [(ngModel)]="currency">
                <option value="USD">USD</option>
                <option value="EUR">EUR</option>
                <option value="GBP">GBP</option>
                <option value="INR">INR</option>
              </select>
            </div>
          </div>

          <!-- Stage -->
          <div class="field">
            <label class="field-label" for="deal-stage">Stage</label>
            <select id="deal-stage" class="field-select" [(ngModel)]="_stageId">
              @for (s of stages(); track s.id) {
                @if (!s.isTerminal) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              }
            </select>
          </div>

          <!-- Owner -->
          <div class="field">
            <label class="field-label" for="deal-owner">Owner</label>
            <select id="deal-owner" class="field-select" [(ngModel)]="ownerId">
              @for (u of users; track u.id) {
                <option [value]="u.id">{{ u.name }}</option>
              }
            </select>
          </div>

          <!-- Contact search -->
          <div class="field" style="position:relative;">
            <label class="field-label" for="deal-contact">Contact (optional)</label>
            <input
              id="deal-contact"
              class="field-input"
              [(ngModel)]="contactSearch"
              (ngModelChange)="onContactSearch($event)"
              (focus)="onContactSearch(contactSearch)"
              placeholder="Search by name or email…"
              autocomplete="off"
            />
            @if (contactResults().length > 0) {
              <div class="autocomplete-dropdown">
                @for (c of contactResults(); track c.id) {
                  <button class="autocomplete-item" (mousedown)="selectContact(c)">
                    <span class="contact-name">{{ c.name }}</span>
                    @if (c.email) { <span class="contact-email">{{ c.email }}</span> }
                  </button>
                }
              </div>
            }
            @if (selectedContact()) {
              <div class="selected-contact-chip">
                <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/></svg>
                {{ selectedContact()!.name }}
                <button class="chip-remove" (click)="clearContact()" aria-label="Remove contact">×</button>
              </div>
            }
          </div>

          <!-- Expected close date -->
          <div class="field">
            <label class="field-label" for="deal-close-date">Expected Close Date</label>
            <input id="deal-close-date" class="field-input" type="date" [(ngModel)]="expectedCloseDate" />
          </div>

          @if (error()) {
            <div class="form-error">{{ error() }}</div>
          }
        </div>

        <div class="modal-footer">
          <button class="btn btn-ghost" (click)="cancelled.emit()">Cancel</button>
          <button
            id="create-deal-submit"
            class="btn btn-solid"
            (click)="submit()"
            [disabled]="!title.trim() || saving()"
          >
            @if (saving()) { Creating… } @else { Create Deal }
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,0.7);
      backdrop-filter: blur(4px);
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 16px;
      animation: fadeIn 150ms ease;
    }
    @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }

    .modal {
      background: var(--bg-3);
      border: 1px solid var(--border-md);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow);
      width: 100%;
      max-width: 480px;
      display: flex;
      flex-direction: column;
      animation: slideUp 180ms cubic-bezier(0.16,1,0.3,1);
      overflow: hidden;
    }
    @keyframes slideUp { from { transform: translateY(16px); opacity: 0; } to { transform: translateY(0); opacity: 1; } }

    .modal-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 18px 20px 14px;
      border-bottom: 1px solid var(--border);
    }
    .modal-title { font-size: .95rem; font-weight: 700; color: var(--text); letter-spacing: -.02em; }
    .close-btn {
      width: 28px; height: 28px;
      background: none;
      border: 1px solid var(--border);
      border-radius: 6px;
      color: var(--text-3);
      display: flex; align-items: center; justify-content: center;
      cursor: pointer;
      transition: background 150ms, color 150ms;
    }
    .close-btn:hover { background: var(--bg-hover); color: var(--text); }

    .modal-body {
      padding: 18px 20px;
      display: flex;
      flex-direction: column;
      gap: 14px;
      max-height: 70vh;
      overflow-y: auto;
    }

    .field { display: flex; flex-direction: column; gap: 5px; }
    .field-row { display: flex; gap: 12px; }
    .flex-2 { flex: 2; }
    .flex-1 { flex: 1; }

    .field-label { font-size: .72rem; font-weight: 600; color: var(--text-3); text-transform: uppercase; letter-spacing: .06em; }
    .required { color: var(--red); }

    .field-input, .field-select {
      width: 100%;
      padding: 8px 11px;
      background: var(--bg-2);
      border: 1px solid var(--border-md);
      border-radius: 6px;
      font-family: var(--font);
      font-size: .83rem;
      color: var(--text);
      outline: none;
      transition: border-color 150ms;
      appearance: none;
    }
    .field-input::placeholder { color: var(--text-4); }
    .field-input:focus, .field-select:focus { border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-dim); }
    .field-select { cursor: pointer; background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='%23666' stroke-width='2.5' stroke-linecap='round'%3E%3Cpolyline points='6 9 12 15 18 9'/%3E%3C/svg%3E"); background-repeat: no-repeat; background-position: right 10px center; padding-right: 30px; }
    .input-prefix-wrap { position: relative; }
    .input-prefix { position: absolute; left: 11px; top: 50%; transform: translateY(-50%); color: var(--text-3); font-size: .83rem; pointer-events: none; }
    .field-input.prefixed { padding-left: 22px; }

    .autocomplete-dropdown {
      position: absolute;
      top: calc(100% + 4px);
      left: 0; right: 0;
      background: var(--bg-3);
      border: 1px solid var(--border-md);
      border-radius: var(--radius);
      box-shadow: var(--shadow);
      z-index: 10;
      overflow: hidden;
    }
    .autocomplete-item {
      width: 100%; padding: 9px 12px; text-align: left;
      background: none; border: none; cursor: pointer;
      display: flex; flex-direction: column; gap: 2px;
      transition: background 120ms;
    }
    .autocomplete-item:hover { background: var(--bg-hover); }
    .contact-name { font-size: .82rem; color: var(--text); font-weight: 500; }
    .contact-email { font-size: .72rem; color: var(--text-3); }

    .selected-contact-chip {
      display: inline-flex; align-items: center; gap: 6px;
      background: var(--accent-dim);
      border: 1px solid rgba(99,102,241,0.3);
      color: #a5b4fc;
      border-radius: 20px;
      padding: 3px 10px 3px 8px;
      font-size: .76rem; font-weight: 500; margin-top: 6px;
    }
    .chip-remove { background: none; border: none; color: #a5b4fc; cursor: pointer; font-size: 1rem; line-height: 1; padding: 0; opacity: 0.7; }
    .chip-remove:hover { opacity: 1; }

    .form-error {
      background: var(--red-dim);
      border: 1px solid rgba(248,113,113,0.3);
      color: var(--red);
      border-radius: 6px;
      padding: 9px 12px;
      font-size: .8rem;
    }

    .modal-footer {
      display: flex; justify-content: flex-end; gap: 8px;
      padding: 14px 20px;
      border-top: 1px solid var(--border);
      background: var(--bg-2);
    }
  `]
})
export class CreateDealModalComponent implements OnInit {
  // Angular 20: signal inputs
  stages = input<Stage[]>([]);
  defaultStageId = input<string>('');

  // Angular 20: signal outputs
  saved = output<any>();
  cancelled = output<void>();

  users = KNOWN_USERS;

  title = '';
  value: number = 0;
  currency = 'USD';
  ownerId = KNOWN_USERS[0].id;
  contactSearch = '';
  expectedCloseDate = '';

  // Angular 20: linkedSignal — auto-resets to defaultStageId() whenever the parent changes it
  // but remains writable so the user can change it in the dropdown
  _stageId = linkedSignal(() => this.defaultStageId() || this.stages().find(s => !s.isTerminal)?.id || '');

  contactResults = signal<any[]>([]);
  selectedContact = signal<{ id: string; name: string } | null>(null);
  saving = signal(false);
  error = signal<string | null>(null);

  private _searchTimer: any;

  constructor(private dealsService: DealsService) {}

  ngOnInit(): void {}

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.cancelled.emit();
    }
  }

  onContactSearch(val: string): void {
    clearTimeout(this._searchTimer);
    if (!val.trim() || this.selectedContact()) { this.contactResults.set([]); return; }
    this._searchTimer = setTimeout(async () => {
      try {
        const results = await this.dealsService.searchContacts(val.trim());
        this.contactResults.set(results.slice(0, 6));
      } catch { this.contactResults.set([]); }
    }, 250);
  }

  selectContact(c: any): void {
    this.selectedContact.set({ id: c.id, name: c.name });
    this.contactSearch = '';
    this.contactResults.set([]);
  }

  clearContact(): void {
    this.selectedContact.set(null);
    this.contactSearch = '';
  }

  async submit(): Promise<void> {
    const trimmed = this.title.trim();
    if (!trimmed) return;

    this.saving.set(true);
    this.error.set(null);

    try {
      const body = {
        title: trimmed,
        value: this.value ?? 0,
        currency: this.currency,
        stageId: this._stageId(),
        ownerId: this.ownerId,
        contactId: this.selectedContact()?.id ?? null,
        expectedCloseDate: this.expectedCloseDate || null,
      };
      const deal = await this.dealsService.createDeal(body);
      this.saved.emit(deal);
    } catch (err: any) {
      this.error.set(err?.error?.title ?? 'Failed to create deal. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }
}
