import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface Notification {
  id: string;
  type: string;
  message: string;
  referenceId?: string;
  referenceType?: string;
  readAt?: string | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private base = '/api';

  constructor(private http: HttpClient) {}

  getNotifications(unreadOnly = false): Promise<Notification[]> {
    let params = new HttpParams();
    if (unreadOnly) {
      params = params.set('unreadOnly', 'true');
    }
    return firstValueFrom(
      this.http.get<Notification[]>(`${this.base}/notifications`, { params })
    );
  }

  getUnreadCount(): Promise<{ count: number }> {
    return firstValueFrom(
      this.http.get<{ count: number }>(`${this.base}/notifications/unread-count`)
    );
  }

  markRead(id: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.base}/notifications/${id}/read`, {})
    );
  }

  markAllRead(): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.base}/notifications/read-all`, {})
    );
  }

  deleteNotification(id: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.base}/notifications/${id}`)
    );
  }
}
