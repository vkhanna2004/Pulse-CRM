import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  isLight = signal(false);

  constructor() {
    const saved = localStorage.getItem('pulse-theme');
    if (saved === 'light') this.apply(true);
  }

  toggle(): void {
    this.apply(!this.isLight());
  }

  private apply(light: boolean): void {
    this.isLight.set(light);
    document.body.classList.toggle('light', light);
    localStorage.setItem('pulse-theme', light ? 'light' : 'dark');
  }
}
