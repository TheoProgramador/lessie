import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface CreditPlan {
  code: string;
  name: string;
  description: string;
  credits: number;
  price: number;
  currencyId: string;
  badge: string;
}

export interface CreatePaymentPreferenceRequest {
  planCode: string;
  promotionCode?: string | null;
}

export interface CreatePaymentPreferenceResponse {
  orderId: string;
  preferenceId: string;
  checkoutUrl: string;
  initPoint: string;
  sandboxInitPoint: string;
  publicKey: string;
  originalAmount: number;
  discountAmount: number;
  finalAmount: number;
  credits: number;
  bonusCredits: number;
  currencyId: string;
}

@Injectable({
  providedIn: 'root'
})
export class PaymentsService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = environment.apiBaseUrl;

  getCreditPlans(): Observable<CreditPlan[]> {
    return this.http.get<CreditPlan[]>(`${this.apiBaseUrl}/api/payments/credit-plans`);
  }

  createCheckout(request: CreatePaymentPreferenceRequest): Observable<CreatePaymentPreferenceResponse> {
    return this.http.post<CreatePaymentPreferenceResponse>(`${this.apiBaseUrl}/api/payments/checkout`, request);
  }
}
