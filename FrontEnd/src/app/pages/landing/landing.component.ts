import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CreditPlan, PaymentsService } from 'src/app/services/payments.service';

interface FeatureItem {
  icon: string;
  title: string;
  text: string;
}

interface StepItem {
  label: string;
  title: string;
  text: string;
}

interface ImprovementItem {
  title: string;
  text: string;
}

@Component({
  selector: 'app-landing',
  imports: [CommonModule, RouterModule],
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.scss']
})
export class LandingComponent implements OnInit {
  private readonly paymentsService = inject(PaymentsService);
  private readonly changeDetectorRef = inject(ChangeDetectorRef);

  creditPlans: CreditPlan[] = [];
  loadingCreditPlans = true;

  readonly features: FeatureItem[] = [
    {
      icon: 'feather icon-file-text',
      title: 'Curriculo mais forte',
      text: 'A Lessie le seu curriculo, entende a vaga e mostra como deixar o texto mais claro, objetivo e alinhado ao que recrutadores procuram.'
    },
    {
      icon: 'feather icon-search',
      title: 'Busca de vagas com contexto',
      text: 'Voce pesquisa oportunidades por cargo, tecnologia e localidade, e recebe resultados organizados para comparar com menos perda de tempo.'
    },
    {
      icon: 'feather icon-users',
      title: 'Pessoas certas para abordar',
      text: 'A ferramenta ajuda a encontrar profissionais, recrutadores e publicacoes relacionadas ao seu objetivo de carreira.'
    },
    {
      icon: 'feather icon-mic',
      title: 'Entrevistas melhores',
      text: 'Ao enviar um audio de entrevista, a Lessie transcreve a conversa e aponta sinais de comunicacao, pontos fortes e melhorias praticas.'
    }
  ];

  readonly steps: StepItem[] = [
    {
      label: '01',
      title: 'Entre com Google',
      text: 'Sem criar senha nova. Voce acessa com sua conta Google e configura suas chaves de IA quando precisar.'
    },
    {
      label: '02',
      title: 'Envie seu material',
      text: 'Pode ser curriculo, texto de vaga, prints, audio de entrevista ou palavras-chave de busca.'
    },
    {
      label: '03',
      title: 'Receba uma leitura guiada',
      text: 'A Lessie transforma arquivos e pesquisas em recomendações simples, com proximos passos claros.'
    },
    {
      label: '04',
      title: 'Ajuste e acompanhe',
      text: 'Voce edita o curriculo, exporta versoes, salva historicos e acompanha seus limites de uso.'
    }
  ];

  readonly improvements: ImprovementItem[] = [
    {
      title: 'Mais cobertura com menos scraping',
      text: 'A busca de vagas agora pode usar jd-intel para ler vagas direto de APIs publicas de ATS como Greenhouse, Lever, Ashby, Workday, SmartRecruiters, Teamtailor e Recruitee.'
    },
    {
      title: 'Menos vagas repetidas',
      text: 'A deduplicacao reconhece IDs estaveis de mais plataformas, entao a mesma oportunidade tende a aparecer uma vez mesmo quando vem por fontes diferentes.'
    },
    {
      title: 'Curriculo analisado por mais sinais',
      text: 'RChilli, FormaCV e CV Forge entram como camadas opcionais para parsing, leitura de vaga, score curriculo-vaga, tailoring e estrutura de pacote de candidatura.'
    },
    {
      title: 'Privacidade sob controle',
      text: 'As integracoes externas de curriculo ficam desligadas por padrao e so entram quando configuradas, evitando envio acidental de dados sensiveis.'
    }
  ];

  ngOnInit(): void {
    this.paymentsService.getCreditPlans().subscribe({
      next: (plans) => {
        this.creditPlans = plans;
        this.loadingCreditPlans = false;
        this.changeDetectorRef.detectChanges();
      },
      error: () => {
        this.loadingCreditPlans = false;
        this.changeDetectorRef.detectChanges();
      }
    });
  }

  formatPrice(plan: CreditPlan): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: plan.currencyId || 'BRL'
    }).format(plan.price);
  }
}
