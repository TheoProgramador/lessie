import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export type ChatRole = 'user' | 'assistant' | 'system';

export interface ChatMessage {
  role: ChatRole;
  content: string;
  toolResult?: unknown;
}

export interface ChatbotMessageRequest {
  message: string;
  history: ChatMessage[];
}

export interface ChatbotMessageResponse {
  message: string;
  toolResult?: unknown;
}

@Injectable({
  providedIn: 'root'
})
export class ChatbotService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiBaseUrl;

  sendMessage(request: ChatbotMessageRequest): Observable<ChatbotMessageResponse> {
    return this.http.post<ChatbotMessageResponse>(`${this.apiBaseUrl}/api/chatbot/message`, request);
  }

  sendPollinationsMessage(request: ChatbotMessageRequest): Observable<ChatbotMessageResponse> {
    return this.http.post<ChatbotMessageResponse>(`${this.apiBaseUrl}/api/chatbot/pollinations/message`, request);
  }
}
