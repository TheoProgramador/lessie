import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { catchError, of } from 'rxjs';
import { AuthService, UserProfile } from 'src/app/services/auth.service';
import { CreditPlan, PaymentsService } from 'src/app/services/payments.service';
import { TokenService } from 'src/app/services/token.service';

@Component({
  selector: 'app-credits',
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './credits.component.html',
  styleUrls: ['./credits.component.scss']
})
export class CreditsComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly paymentsService = inject(PaymentsService);
  private readonly tokenService = inject(TokenService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly changeDetectorRef = inject(ChangeDetectorRef);

  profile?: UserProfile;
  loadingProfile = true;
  loadingPlans = true;
  selectedPlanId = '';
  processingPlanId = '';
  promotionCode = '';
  checkoutMessage = '';
  plans: CreditPlan[] = [];

  ngOnInit(): void {
    this.readPaymentReturnStatus();
    this.loadPlans();

    if (!this.tokenService.getAccessToken() && !this.tokenService.getRefreshToken()) {
      this.loadingProfile = false;
      return;
    }

    this.authService
      .me()
      .pipe(catchError(() => of(undefined)))
      .subscribe((profile) => {
        this.profile = profile;
        this.loadingProfile = false;
        this.changeDetectorRef.detectChanges();
      });
  }

  highlightsFor(plan: CreditPlan): string[] {
    const highlights: Record<string, string[]> = {
      starter: ['Analises de curriculo', 'Busca de vagas', 'Historico no painel'],
      focus: ['Mais buscas com IA', 'Entrevistas e curriculos', 'Credito com validade maior'],
      pro: ['Pacote para alto volume', 'Mais margem para testes', 'Pronto para times pequenos']
    };

    return highlights[plan.code] ?? ['Creditos adicionados ao saldo', 'Uso nas ferramentas Lessie', 'Compra registrada no painel'];
  }

  formatPrice(plan: CreditPlan): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: plan.currencyId || 'BRL'
    }).format(plan.price);
  }

  get isPublicPage(): boolean {
    return this.router.url.startsWith('/comprar-creditos');
  }

  get remainingCreditsText(): string {
    if (!this.profile) {
      return 'Entre para ver seus limites atuais';
    }

    if (this.profile.creditBalance > 0) {
      return `${this.profile.creditBalance} creditos disponiveis`;
    }

    const resumeRemaining = Math.max(this.profile.resumeAnalysisLimit - this.profile.resumeAnalysisCount, 0);
    const chatRemaining = Math.max(this.profile.chatConversationLimit - this.profile.chatConversationCount, 0);
    const interviewRemaining = Math.max(this.profile.interviewAnalysisLimit - this.profile.interviewAnalysisCount, 0);

    return `${resumeRemaining} curriculos, ${chatRemaining} chats e ${interviewRemaining} entrevistas disponiveis`;
  }

  selectPlan(plan: CreditPlan): void {
    this.selectedPlanId = plan.code;
    this.checkoutMessage = '';

    if (!this.profile) {
      this.checkoutMessage = 'Entre com sua conta para finalizar a compra deste pacote.';
      return;
    }

    this.processingPlanId = plan.code;
    this.paymentsService
      .createCheckout({
        planCode: plan.code,
        promotionCode: this.promotionCode.trim() || null
      })
      .subscribe({
        next: (response) => {
          this.processingPlanId = '';
          window.location.href = response.checkoutUrl;
        },
        error: (error) => {
          this.processingPlanId = '';
          this.checkoutMessage = error?.error?.message ?? 'Nao foi possivel criar o checkout agora.';
          this.changeDetectorRef.detectChanges();
        }
      });
  }

  private loadPlans(): void {
    this.paymentsService.getCreditPlans().subscribe({
      next: (plans) => {
        this.plans = plans;
        this.loadingPlans = false;
        this.changeDetectorRef.detectChanges();
      },
      error: () => {
        this.checkoutMessage = 'Nao foi possivel carregar os pacotes de creditos.';
        this.loadingPlans = false;
        this.changeDetectorRef.detectChanges();
      }
    });
  }

  private readPaymentReturnStatus(): void {
    const paymentStatus = this.route.snapshot.queryParamMap.get('payment');
    if (paymentStatus === 'success') {
      this.checkoutMessage = 'Pagamento aprovado no Mercado Pago. A liberacao dos creditos entra no proximo passo com webhook.';
    } else if (paymentStatus === 'pending') {
      this.checkoutMessage = 'Pagamento em analise ou aguardando confirmacao no Mercado Pago.';
    } else if (paymentStatus === 'failure') {
      this.checkoutMessage = 'Pagamento nao concluido. Voce pode tentar novamente com outro metodo.';
    }
  }
}
