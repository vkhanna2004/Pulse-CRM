import { Component } from '@angular/core';

@Component({
  selector: 'app-contacts',
  standalone: true,
  template: `
    <div style="padding: 24px;">
      <h2 style="margin: 0 0 8px 0; font-size: 1.25rem; font-weight: 600; color: var(--text);">Contacts</h2>
      <p style="color: var(--text-2); font-size: 0.9rem;">Contact management is coming soon.</p>
    </div>
  `,
  styles: [`
    :host { display: block; height: 100%; box-sizing: border-box; }
  `]
})
export class ContactsComponent {}
