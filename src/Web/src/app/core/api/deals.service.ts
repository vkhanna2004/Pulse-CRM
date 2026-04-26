import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class DealsService {
  private base = '/api';

  constructor(private http: HttpClient) {}

  getPipeline(): Promise<any> {
    return firstValueFrom(this.http.get<any>(`${this.base}/deals/pipelines/default`));
  }

  getDeal(id: string): Promise<any> {
    return firstValueFrom(this.http.get<any>(`${this.base}/deals/${id}`));
  }

  createDeal(body: any): Promise<any> {
    return firstValueFrom(this.http.post<any>(`${this.base}/deals`, body));
  }

  updateDeal(id: string, body: any): Promise<any> {
    return firstValueFrom(this.http.patch<any>(`${this.base}/deals/${id}`, body));
  }

  moveDeal(id: string, body: { stageId: string; positionInStage: number; rowVersion: number }): Observable<any> {
    return this.http.post<any>(`${this.base}/deals/${id}/move`, body);
  }

  addNote(dealId: string, content: string): Promise<any> {
    return firstValueFrom(this.http.post<any>(`${this.base}/deals/${dealId}/notes`, { content }));
  }

  logActivity(dealId: string, type: string, content: string): Promise<any> {
    return firstValueFrom(this.http.post<any>(`${this.base}/deals/${dealId}/activities`, { type, content }));
  }

  searchContacts(search: string): Promise<any[]> {
    return firstValueFrom(this.http.get<any[]>(`${this.base}/contacts?search=${encodeURIComponent(search)}`));
  }

  recalculateScore(dealId: string): Promise<any> {
    return firstValueFrom(this.http.post<any>(`${this.base}/deals/${dealId}/score:recalculate`, {}));
  }

  getSummary(dealId: string): Promise<any> {
    return firstValueFrom(this.http.get<any>(`${this.base}/ai/deals/${dealId}/summary`));
  }

  regenerateSummary(dealId: string): Promise<any> {
    return firstValueFrom(this.http.post<any>(`${this.base}/ai/deals/${dealId}/summary:regenerate`, {}));
  }
}
