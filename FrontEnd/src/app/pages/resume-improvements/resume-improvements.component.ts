import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import Quill from 'quill';
import { AuthService } from 'src/app/services/auth.service';
import { ChatMessage } from 'src/app/services/chatbot.service';
import {
  ResumeAtsAnalysis,
  ResumeImprovementChatResponse,
  ResumeImprovementHistoryItem,
  ResumeImprovementsService
} from 'src/app/services/resume-improvements.service';
import { SharedModule } from 'src/app/theme/shared/shared.module';

type PreviewLineKind = 'heading' | 'subheading' | 'bullet' | 'numbered' | 'paragraph';

interface PreviewLine {
  kind: PreviewLineKind;
  text: string;
}

@Component({
  selector: 'app-resume-improvements',
  imports: [CommonModule, FormsModule, SharedModule],
  templateUrl: './resume-improvements.component.html',
  styleUrls: ['./resume-improvements.component.scss']
})
export class ResumeImprovementsComponent implements OnInit, OnDestroy {
  @ViewChild('conversationList') private conversationList?: ElementRef<HTMLDivElement>;
  @ViewChild('quillEditor')
  set quillEditorRef(value: ElementRef<HTMLDivElement> | undefined) {
    this.quillEditor = value;
    if (value) {
      this.queueEditorInitialization();
    }
  }

  private readonly resumeService = inject(ResumeImprovementsService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly cd = inject(ChangeDetectorRef);

  resumeFile: File | null = null;
  linkedinProfileFile: File | null = null;
  jobScreenshots: File[] = [];
  linkedinProfileUrl = '';
  githubProfileUrl = '';
  portfolioUrl = '';
  personalInfo = '';
  customInstructions = '';
  jobDescription = '';
  resumeText = '';
  jobContext = '';
  optimizedResume = '';
  messageInput = '';
  loading = false;
  analyzing = false;
  exporting = false;
  error = '';
  readyToExport = false;
  atsAnalysis: ResumeAtsAnalysis | null = null;
  messages: ChatMessage[] = [];
  sessionId = '';
  history: ResumeImprovementHistoryItem[] = [];
  loadingHistory = false;
  loadingHistoryList = false;
  historyLoaded = false;
  historyError = '';
  editorHtml = '';
  savingEditor = false;
  editorSaveStatus = '';
  profileLinksSaveStatus = '';
  renamingSessionId = '';
  renameTitle = '';
  savingRename = false;
  resumeAnalysisCount = 0;
  resumeAnalysisLimit = 20;

  private editorSaveTimer?: ReturnType<typeof setTimeout>;
  private profileLinksSaveTimer?: ReturnType<typeof setTimeout>;
  private lastSavedResume = '';
  private lastSavedProfileLinks = '';
  private sessionLoadedFromHistory = false;
  private quillEditor?: ElementRef<HTMLDivElement>;
  private editorInitializationTimer?: ReturnType<typeof setTimeout>;
  private quill?: Quill;
  private quillEditorElement?: HTMLDivElement;
  private syncingQuill = false;
  private selectedMissingKeywords = new Set<string>();

  get optimizedPreviewLines(): PreviewLine[] {
    return this.formatPreviewLines(this.optimizedResume);
  }

  get selectedMissingKeywordList(): string[] {
    return Array.from(this.selectedMissingKeywords);
  }

  get canAnalyze(): boolean {
    return Boolean(this.resumeFile) || Boolean(this.sessionId && this.jobScreenshots.length > 0);
  }

  get analyzeButtonLabel(): string {
    return this.resumeFile || !this.sessionId ? 'Analisar curriculo' : 'Otimizar com prints';
  }

  get aiIsWorking(): boolean {
    return this.loading || this.analyzing;
  }

  get aiWorkingMessage(): string {
    if (this.loading) {
      return 'Integrando suas respostas, o diagnostico ATS do CV Mirror e atualizando a versao otimizada...';
    }

    if (this.analyzing && !this.resumeFile && this.jobScreenshots.length > 0) {
      return 'Pesquisando boas praticas online, lendo os prints e cruzando com o diagnostico ATS salvo...';
    }

    if (this.analyzing && this.jobScreenshots.length > 0) {
      return 'Analisando o curriculo com CV Mirror MCP, contextos adicionais, boas praticas e prints de vaga...';
    }

    return 'Analisando compatibilidade ATS, contextos adicionais, boas praticas e a versao enviada...';
  }

  get atsScoreClass(): string {
    const score = this.atsAnalysis?.overallScore ?? 0;
    if (score >= 80) {
      return 'ats-score-good';
    }

    if (score >= 65) {
      return 'ats-score-warning';
    }

    return 'ats-score-critical';
  }

  formatAtsLabel(value: unknown): string {
    return String(value).replace(/_/g, ' ');
  }

  ngOnInit(): void {
    this.loadHistory();
    this.loadUsage();
  }

  ngOnDestroy(): void {
    this.clearEditorSaveTimer();
    this.clearProfileLinksSaveTimer();
    this.clearEditorInitializationTimer();
    this.destroyQuillEditor();
  }

  onResumeSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.resumeFile = input.files?.[0] ?? null;
    this.error = '';
  }

  onLinkedInProfileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.linkedinProfileFile = input.files?.[0] ?? null;
    this.error = '';
  }

  onScreenshotsSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.setJobScreenshots(Array.from(input.files ?? []));
    this.error = '';
  }

  onComposerPaste(event: ClipboardEvent): void {
    const files = Array.from(event.clipboardData?.items ?? [])
      .filter((item) => item.kind === 'file' && item.type.startsWith('image/'))
      .map((item) => item.getAsFile())
      .filter((file): file is File => Boolean(file))
      .map((file, index) => this.renameClipboardImage(file, index));

    if (files.length === 0) {
      return;
    }

    event.preventDefault();
    this.setJobScreenshots([...this.jobScreenshots, ...files]);
    this.error = '';
    this.cd.detectChanges();
  }

  analyze(): void {
    if (this.analyzing) {
      return;
    }

    if (!this.resumeFile) {
      this.optimizeLoadedSessionForJob();
      return;
    }

    this.error = '';
    this.analyzing = true;
    this.messages = [];
    this.setOptimizedResume('');
    this.atsAnalysis = null;
    this.sessionId = '';
    this.sessionLoadedFromHistory = false;

    this.resumeService.analyze(this.resumeFile, this.jobScreenshots, {
      linkedinProfile: this.linkedinProfileFile,
      linkedinProfileUrl: this.linkedinProfileUrl.trim(),
      githubProfileUrl: this.githubProfileUrl.trim(),
      portfolioUrl: this.portfolioUrl.trim(),
      personalInfo: this.personalInfo.trim(),
      customInstructions: this.customInstructions.trim(),
      jobDescription: this.jobDescription.trim()
    }).subscribe({
      next: (response) => {
        this.analyzing = false;
        this.sessionId = response.sessionId;
        this.sessionLoadedFromHistory = false;
        this.resumeText = response.resumeText;
        this.jobContext = response.jobContext;
        this.lastSavedProfileLinks = this.currentProfileLinksKey();
        this.setOptimizedResume(response.optimizedResume);
        this.lastSavedResume = response.optimizedResume;
        this.readyToExport = response.readyToExport;
        this.atsAnalysis = this.normalizeAtsAnalysis(response.atsAnalysis ?? null);
        this.messages = [{ role: 'assistant', content: response.message }];
        this.loadUsage();
        this.loadHistory();
        this.cd.detectChanges();
        this.scrollToBottom();
      },
      error: (error: HttpErrorResponse) => {
        this.analyzing = false;
        this.error = error.error?.message ?? 'Nao foi possivel analisar o curriculo.';
        this.cd.detectChanges();
      }
    });
  }

  sendMessage(): void {
    const message = this.messageInput.trim();
    if (!message || this.loading) {
      return;
    }

    this.sendResumeImprovementMessage(message);
  }

  optimizeFromAtsSuggestions(): void {
    if (!this.atsAnalysis || !this.optimizedResume || this.loading || this.analyzing) {
      return;
    }

    const recommendations = this.atsAnalysis.recommendations.slice(0, 8).join('; ') || 'aplicar melhorias gerais do diagnostico ATS';
    const gaps = this.atsAnalysis.criticalGaps.slice(0, 6).join('; ') || 'sem gaps criticos estruturados';
    const missingKeywords = this.atsAnalysis.keywordsMissing.slice(0, 12).join(', ') || 'sem keywords ausentes criticas';
    const selectedMissingKeywords = this.selectedMissingKeywordList.join(', ');
    const message = [
      'Aplique automaticamente as sugestoes do Score ATS no curriculo atual, sem fazer novas perguntas.',
      'Use apenas informacoes verdadeiras ja presentes no curriculo, historico, links ou contexto da vaga.',
      selectedMissingKeywords
        ? `Keywords ausentes selecionadas pelo usuario para tentar incluir: ${selectedMissingKeywords}.`
        : 'Nenhuma keyword ausente foi selecionada pelo usuario; nao tente incluir automaticamente keywords ausentes, use-as apenas como contexto de gaps.',
      'Inclua uma keyword selecionada somente se houver evidencia real no curriculo, historico, links ou contexto da vaga.',
      'Se alguma keyword ausente nao tiver evidencia real, nao invente: apenas melhore o texto com o que for comprovado e explique o gap fora dos marcadores.',
      `Score atual: ${this.atsAnalysis.overallScore}/100.`,
      `Recomendacao de matching: ${this.atsAnalysis.matchRecommendation || 'otimizar'}.`,
      `Gaps criticos: ${gaps}.`,
      `Keywords ausentes: ${missingKeywords}.`,
      `Sugestoes ATS: ${recommendations}.`,
      'Devolva obrigatoriamente o curriculo completo atualizado entre [CURRICULO_OTIMIZADO] e [/CURRICULO_OTIMIZADO] para que o sistema meca novamente.'
    ].join('\n');

    this.sendResumeImprovementMessage(message);
  }

  optimizeKeywordStrategy(): void {
    if (!this.atsAnalysis || !this.optimizedResume || this.loading || this.analyzing) {
      return;
    }

    const strategy = (this.atsAnalysis.keywordStrategy ?? [])
      .map((item) => {
        const keywords = item.keywords.slice(0, 12).join(', ');
        return `${item.group} -> ${item.targetSection}: ${keywords}. ${item.instruction}`;
      })
      .join('\n');
    const coverage = (this.atsAnalysis.requirementCoverage ?? [])
      .slice(0, 8)
      .map((item) => `${item.status}: ${item.requirement} (${item.evidence})`)
      .join('\n');
    const selectedMissingKeywords = this.selectedMissingKeywordList.join(', ');
    const message = [
      'Execute uma otimizacao automatica por palavras-chave ATS, sem fazer novas perguntas.',
      'Aplique o mapa de keywords por secao abaixo com densidade natural, sem keyword stuffing.',
      'Use keywords obrigatorias no resumo e experiencia quando houver evidencia real.',
      'Use keywords tecnicas literalmente na secao de competencias e reforce nos bullets apenas quando comprovado.',
      'Use dominio e senioridade no titulo/resumo apenas se forem verdadeiros.',
      'Nao invente experiencias, ferramentas, senioridade, empresas, numeros ou resultados.',
      selectedMissingKeywords
        ? `Keywords ausentes selecionadas pelo usuario para tentar incluir: ${selectedMissingKeywords}.`
        : 'Nenhuma keyword ausente foi selecionada pelo usuario; nao inclua keywords ausentes automaticamente.',
      'Se uma keyword nao tiver evidencia, nao inclua no curriculo; deixe apenas implícito como gap na resposta fora dos marcadores.',
      `Mapa de keywords:\n${strategy || 'Sem mapa estruturado; use keywords ausentes e recomendacoes do ATS.'}`,
      `Cobertura de requisitos:\n${coverage || 'Sem cobertura de requisitos estruturada.'}`,
      'Devolva obrigatoriamente o curriculo completo atualizado entre [CURRICULO_OTIMIZADO] e [/CURRICULO_OTIMIZADO] para recalcular o ATS.'
    ].join('\n');

    this.sendResumeImprovementMessage(message);
  }

  openJobSearch(keyword: string): void {
    const keywords = keyword.trim();
    if (!keywords) {
      return;
    }

    this.router.navigate(['/people-discovery/jobs'], {
      queryParams: { keywords, autoSearch: '1' }
    });
  }

  openJobSearchWithTopKeywords(): void {
    const keywords = (this.atsAnalysis?.jobSearchKeywords ?? [])
      .slice(0, 4)
      .join(' ')
      .trim();
    if (!keywords) {
      return;
    }

    this.openJobSearch(keywords);
  }

  toggleMissingKeyword(keyword: string): void {
    const normalized = keyword.trim();
    if (!normalized || this.loading || this.analyzing) {
      return;
    }

    if (this.selectedMissingKeywords.has(normalized)) {
      this.selectedMissingKeywords.delete(normalized);
    } else {
      this.selectedMissingKeywords.add(normalized);
    }
  }

  isMissingKeywordSelected(keyword: string): boolean {
    return this.selectedMissingKeywords.has(keyword.trim());
  }

  clearSelectedMissingKeywords(): void {
    this.selectedMissingKeywords.clear();
  }

  private sendResumeImprovementMessage(message: string): void {
    const history = [...this.messages];
    this.messages = [...this.messages, { role: 'user', content: message }];
    this.messageInput = '';
    this.loading = true;
    this.error = '';
    this.scrollToBottom();

    this.resumeService
      .chat({
        resumeText: this.resumeText,
        jobContext: this.jobContext,
        optimizedResume: this.optimizedResume,
        sessionId: this.sessionId || undefined,
        forkFromSession: this.sessionLoadedFromHistory,
        message,
        linkedinProfileUrl: this.linkedinProfileUrl.trim(),
        githubProfileUrl: this.githubProfileUrl.trim(),
        portfolioUrl: this.portfolioUrl.trim(),
        history
      })
      .subscribe({
        next: (response) => {
          this.loading = false;
          this.sessionId = response.sessionId || this.sessionId;
          this.sessionLoadedFromHistory = false;
          this.setOptimizedResume(response.optimizedResume);
          this.lastSavedResume = response.optimizedResume;
          this.readyToExport = response.readyToExport;
          this.atsAnalysis = this.normalizeAtsAnalysis(response.atsAnalysis ?? this.atsAnalysis);
          this.appendImprovementResponseMessages(response);
          this.loadHistory();
          this.cd.detectChanges();
          this.scrollToBottom();
        },
        error: (error: HttpErrorResponse) => {
          this.loading = false;
          this.error = error.error?.message ?? 'Nao foi possivel continuar a melhoria do curriculo.';
          this.cd.detectChanges();
          this.scrollToBottom();
        }
      });
  }

  private appendImprovementResponseMessages(response: ResumeImprovementChatResponse): void {
    const nextMessages: ChatMessage[] = [];
    const sentPayloadPreview = response.sentPayloadPreview?.trim();
    if (sentPayloadPreview) {
      nextMessages.push({
        role: 'assistant',
        content: `Enviado para IA nesta rodada:\n\n${sentPayloadPreview}`
      });
    }

    nextMessages.push({
      role: 'assistant',
      content: this.normalizeAssistantDisplayMessage(response.message)
    });

    this.messages = [...this.messages, ...nextMessages];
  }

  private normalizeAssistantDisplayMessage(message: string): string {
    const normalized = (message || '').replace(/\s+/g, ' ').trim();
    const words = normalized.match(/[\p{L}\p{N}]+/gu)?.length ?? 0;
    if (normalized.length < 12 || words < 3) {
      return 'Preparei a otimizacao com os dados enviados. Confira a versao atualizada do curriculo e o Score ATS recalculado.';
    }

    return normalized;
  }

  export(format: 'docx' | 'pdf'): void {
    if (!this.optimizedResume || this.exporting) {
      return;
    }

    this.exporting = true;
    this.resumeService.export(this.optimizedResume, format).subscribe({
      next: (blob) => {
        this.exporting = false;
        this.download(blob, `curriculo-otimizado.${format}`);
        this.cd.detectChanges();
      },
      error: (error: HttpErrorResponse) => {
        this.exporting = false;
        this.error = error.error?.message ?? 'Nao foi possivel exportar o curriculo.';
        this.cd.detectChanges();
      }
    });
  }

  onComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  trackMessage(index: number): number {
    return index;
  }

  trackPreviewLine(index: number): number {
    return index;
  }

  trackHistoryItem(_: number, item: ResumeImprovementHistoryItem): string {
    return item.id;
  }

  startRename(event: Event, item: ResumeImprovementHistoryItem): void {
    event.stopPropagation();
    if (this.loadingHistory || this.analyzing || this.loading || this.savingRename) {
      return;
    }

    this.renamingSessionId = item.id;
    this.renameTitle = item.title;
  }

  cancelRename(event?: Event): void {
    event?.stopPropagation();
    if (this.savingRename) {
      return;
    }

    this.renamingSessionId = '';
    this.renameTitle = '';
  }

  onRenameKeydown(event: KeyboardEvent, item: ResumeImprovementHistoryItem): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.saveRename(event, item);
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      this.cancelRename(event);
    }
  }

  saveRename(event: Event, item: ResumeImprovementHistoryItem): void {
    event.stopPropagation();
    const title = this.renameTitle.trim();
    if (!title || this.savingRename) {
      return;
    }

    if (title === item.title) {
      this.cancelRename(event);
      return;
    }

    this.savingRename = true;
    this.resumeService.renameSession(item.id, title).subscribe({
      next: (response) => {
        this.savingRename = false;
        this.history = this.history.map((historyItem) =>
          historyItem.id === item.id
            ? {
                ...historyItem,
                title: response.title,
                updatedAt: response.updatedAt
              }
            : historyItem
        );
        this.renamingSessionId = '';
        this.renameTitle = '';
        this.cd.detectChanges();
      },
      error: (error: HttpErrorResponse) => {
        this.savingRename = false;
        this.error = error.error?.message ?? 'Nao foi possivel renomear este historico.';
        this.cd.detectChanges();
      }
    });
  }

  deleteSession(event: Event, sessionId: string): void {
    event.stopPropagation();
    if (!sessionId || !confirm('Apagar este historico de melhoria?')) {
      return;
    }

    this.resumeService.deleteSession(sessionId).subscribe({
      next: () => {
        if (this.sessionId === sessionId) {
          this.sessionId = '';
          this.sessionLoadedFromHistory = false;
          this.resumeText = '';
          this.jobContext = '';
          this.setOptimizedResume('');
          this.lastSavedResume = '';
          this.readyToExport = false;
          this.atsAnalysis = null;
          this.messages = [];
          this.destroyQuillEditor();
        }

        this.loadHistory();
        this.cd.detectChanges();
      },
      error: (error: HttpErrorResponse) => {
        this.error = error.error?.message ?? 'Nao foi possivel apagar este historico.';
        this.cd.detectChanges();
      }
    });
  }

  loadSession(sessionId: string): void {
    if (!sessionId || this.loading || this.analyzing) {
      return;
    }

    this.error = '';
    this.loadingHistory = true;
    this.resumeService.getSession(sessionId).subscribe({
      next: (session) => {
        this.loadingHistory = false;
        this.sessionId = session.id;
        this.sessionLoadedFromHistory = true;
        this.resumeText = session.hasResumeContext || session.optimizedResume ? 'Contexto de curriculo recuperado do historico.' : '';
        this.jobContext = session.jobContext;
        this.linkedinProfileUrl = session.linkedinProfileUrl ?? '';
        this.githubProfileUrl = session.githubProfileUrl ?? '';
        this.portfolioUrl = session.portfolioUrl ?? '';
        this.lastSavedProfileLinks = this.currentProfileLinksKey();
        this.setOptimizedResume(session.optimizedResume);
        this.lastSavedResume = session.optimizedResume;
        this.readyToExport = session.readyToExport;
        this.atsAnalysis = this.normalizeAtsAnalysis(session.atsAnalysis ?? null);
        this.messages = session.messages;
        this.cd.detectChanges();
        this.scrollToBottom();
      },
      error: (error: HttpErrorResponse) => {
        this.loadingHistory = false;
        this.error = error.error?.message ?? 'Nao foi possivel carregar este historico.';
        this.cd.detectChanges();
      }
    });
  }

  formatPreviewLines(content: string): PreviewLine[] {
    return this.stripResumeMarkers(content)
      .replace(/\r\n/g, '\n')
      .split('\n')
      .map((line) => this.parsePreviewLine(line))
      .filter((line): line is PreviewLine => Boolean(line));
  }

  onEditorInput(): void {
    const html = this.quill?.root.innerHTML ?? this.editorHtml;
    this.editorHtml = html;
    this.optimizedResume = this.editorHtmlToMarkdown(html);
    this.readyToExport = Boolean(this.optimizedResume.trim());
    this.scheduleEditorSave();
  }

  saveEditedResumeNow(): void {
    this.clearEditorSaveTimer();
    if (!this.sessionId || this.savingEditor || !this.optimizedResume.trim() || this.optimizedResume === this.lastSavedResume) {
      return;
    }

    this.savingEditor = true;
    this.editorSaveStatus = 'Salvando...';
    this.resumeService.saveOptimizedResume(this.sessionId, this.optimizedResume, this.sessionLoadedFromHistory).subscribe({
      next: (response) => {
        this.savingEditor = false;
        this.sessionId = response.sessionId || this.sessionId;
        this.sessionLoadedFromHistory = false;
        this.lastSavedResume = response.optimizedResume;
        this.readyToExport = response.readyToExport;
        this.atsAnalysis = this.normalizeAtsAnalysis(response.atsAnalysis ?? this.atsAnalysis);
        this.editorSaveStatus = 'Salvo';
        this.loadHistory();
        this.cd.detectChanges();
      },
      error: (error: HttpErrorResponse) => {
        this.savingEditor = false;
        this.editorSaveStatus = '';
        this.error = error.error?.message ?? 'Nao foi possivel salvar as alteracoes.';
        this.cd.detectChanges();
      }
    });
  }

  onProfileLinksChanged(): void {
    if (!this.sessionId || this.analyzing || this.loadingHistory) {
      return;
    }

    this.clearProfileLinksSaveTimer();
    this.profileLinksSaveStatus = 'Salvando links...';
    this.profileLinksSaveTimer = setTimeout(() => this.saveProfileLinksNow(), 700);
  }

  saveProfileLinksNow(): void {
    this.clearProfileLinksSaveTimer();
    if (!this.sessionId) {
      return;
    }

    const currentKey = this.currentProfileLinksKey();
    if (currentKey === this.lastSavedProfileLinks) {
      this.profileLinksSaveStatus = '';
      return;
    }

    const sentLinks = {
      linkedinProfileUrl: this.linkedinProfileUrl.trim(),
      githubProfileUrl: this.githubProfileUrl.trim(),
      portfolioUrl: this.portfolioUrl.trim()
    };
    const sentKey = JSON.stringify(sentLinks);

    this.resumeService
      .updateProfileLinks(this.sessionId, sentLinks.linkedinProfileUrl, sentLinks.githubProfileUrl, sentLinks.portfolioUrl)
      .subscribe({
        next: () => {
          if (this.currentProfileLinksKey() === sentKey) {
            this.lastSavedProfileLinks = sentKey;
            this.profileLinksSaveStatus = 'Links salvos';
          } else {
            this.profileLinksSaveStatus = 'Salvando links...';
            this.onProfileLinksChanged();
          }

          this.loadHistory();
          this.cd.detectChanges();
        },
        error: (error: HttpErrorResponse) => {
          this.profileLinksSaveStatus = '';
          this.error = error.error?.message ?? 'Nao foi possivel salvar os links do perfil.';
          this.cd.detectChanges();
        }
      });
  }

  private parsePreviewLine(rawLine: string): PreviewLine | null {
    const line = rawLine.trim();
    if (!line || line.startsWith('```')) {
      return null;
    }

    const heading = line.match(/^(#{1,6})\s+(.+)$/);
    if (heading) {
      return {
        kind: heading[1].length === 1 ? 'heading' : 'subheading',
        text: this.cleanInlineMarkdown(heading[2])
      };
    }

    const bullet = line.match(/^[-*]\s+(.+)$/);
    if (bullet) {
      return { kind: 'bullet', text: this.cleanInlineMarkdown(bullet[1]) };
    }

    const numbered = line.match(/^(\d+[\.)])\s+(.+)$/);
    if (numbered) {
      return { kind: 'numbered', text: `${numbered[1]} ${this.cleanInlineMarkdown(numbered[2])}` };
    }

    if (line.length <= 80 && line.endsWith(':')) {
      return { kind: 'subheading', text: this.cleanInlineMarkdown(line).replace(/:$/, '') };
    }

    return { kind: 'paragraph', text: this.cleanInlineMarkdown(line) };
  }

  private stripResumeMarkers(content: string): string {
    const optimizedMatch = content.match(/\[CURRICULO_OTIMIZADO\]([\s\S]*?)\[\/CURRICULO_OTIMIZADO\]/i);
    return (optimizedMatch?.[1] ?? content)
      .replace(/\[\/?CURRICULO_OTIMIZADO\]/gi, '')
      .trim();
  }

  private cleanInlineMarkdown(value: string): string {
    return value.replace(/\*\*/g, '').replace(/`/g, '').trim();
  }

  private setOptimizedResume(content: string): void {
    this.optimizedResume = content;
    this.editorHtml = this.markdownToEditorHtml(content);
    this.syncQuillEditor();
    this.queueEditorInitialization();
  }

  private initializeQuillEditor(): void {
    const element = this.quillEditor?.nativeElement;
    if (!element) {
      return;
    }

    if (this.quill && this.quillEditorElement === element) {
      return;
    }

    this.quillEditorElement = element;
    this.quill = new Quill(element, {
      modules: {
        toolbar: [
          [{ header: [1, 2, 3, false] }, { size: ['small', false, 'large', 'huge'] }],
          ['bold', 'italic', 'underline', 'strike'],
          [{ color: [] }, { background: [] }],
          [{ list: 'bullet' }, { list: 'ordered' }, { indent: '-1' }, { indent: '+1' }],
          [{ align: [] }],
          ['link'],
          ['clean']
        ]
      },
      placeholder: 'Edite a versao otimizada do curriculo...',
      theme: 'snow'
    });

    this.quill.on('text-change', () => {
      if (this.syncingQuill) {
        return;
      }

      this.onEditorInput();
    });

    this.syncQuillEditor();
  }

  private queueEditorInitialization(): void {
    this.clearEditorInitializationTimer();
    this.editorInitializationTimer = setTimeout(() => {
      this.editorInitializationTimer = undefined;
      this.initializeQuillEditor();
    });
  }

  private clearEditorInitializationTimer(): void {
    if (this.editorInitializationTimer) {
      clearTimeout(this.editorInitializationTimer);
      this.editorInitializationTimer = undefined;
    }
  }

  private destroyQuillEditor(): void {
    this.clearEditorInitializationTimer();
    this.quill = undefined;
    this.quillEditorElement = undefined;
    this.quillEditor = undefined;
  }

  private syncQuillEditor(): void {
    if (!this.quill) {
      return;
    }

    const html = this.editorHtml || '<p><br></p>';
    if (this.quill.root.innerHTML === html) {
      return;
    }

    this.syncingQuill = true;
    this.quill.clipboard.dangerouslyPasteHTML(html);
    this.syncingQuill = false;
  }

  private scheduleEditorSave(): void {
    this.clearEditorSaveTimer();
    this.editorSaveStatus = this.sessionId ? 'Alteracoes pendentes' : '';
    if (!this.sessionId) {
      return;
    }

    this.editorSaveTimer = setTimeout(() => this.saveEditedResumeNow(), 900);
  }

  private clearEditorSaveTimer(): void {
    if (this.editorSaveTimer) {
      clearTimeout(this.editorSaveTimer);
      this.editorSaveTimer = undefined;
    }
  }

  private clearProfileLinksSaveTimer(): void {
    if (this.profileLinksSaveTimer) {
      clearTimeout(this.profileLinksSaveTimer);
      this.profileLinksSaveTimer = undefined;
    }
  }

  private normalizeAtsAnalysis(analysis: ResumeAtsAnalysis | null): ResumeAtsAnalysis | null {
    if (!analysis) {
      this.clearSelectedMissingKeywords();
      return null;
    }

    const normalized = {
      ...analysis,
      sections: analysis.sections ?? [],
      strengths: analysis.strengths ?? [],
      risks: analysis.risks ?? [],
      recommendations: analysis.recommendations ?? [],
      keywordsPresent: analysis.keywordsPresent ?? [],
      keywordsMissing: analysis.keywordsMissing ?? [],
      keywordsPartial: analysis.keywordsPartial ?? [],
      jobSearchKeywords: analysis.jobSearchKeywords ?? [],
      criticalGaps: analysis.criticalGaps ?? [],
      matchRecommendation: analysis.matchRecommendation ?? '',
      subscores: analysis.subscores ?? {},
      requirementCoverage: analysis.requirementCoverage ?? [],
      keywordStrategy: analysis.keywordStrategy ?? []
    };

    const availableMissingKeywords = new Set(normalized.keywordsMissing);
    this.selectedMissingKeywords = new Set(
      Array.from(this.selectedMissingKeywords).filter((keyword) => availableMissingKeywords.has(keyword))
    );

    return normalized;
  }

  private currentProfileLinksKey(): string {
    return JSON.stringify({
      linkedinProfileUrl: this.linkedinProfileUrl.trim(),
      githubProfileUrl: this.githubProfileUrl.trim(),
      portfolioUrl: this.portfolioUrl.trim()
    });
  }

  private setJobScreenshots(files: File[]): void {
    this.jobScreenshots = files.filter((file) => file.type.startsWith('image/')).slice(0, 4);
  }

  private renameClipboardImage(file: File, index: number): File {
    const extension = this.getImageExtension(file);
    const timestamp = new Date().toISOString().replace(/[-:TZ.]/g, '').slice(0, 14);
    return new File([file], `print-colado-${timestamp}-${index + 1}.${extension}`, {
      type: file.type || `image/${extension}`,
      lastModified: Date.now()
    });
  }

  private getImageExtension(file: File): string {
    if (file.type.includes('jpeg')) {
      return 'jpg';
    }

    if (file.type.includes('webp')) {
      return 'webp';
    }

    return 'png';
  }

  private markdownToEditorHtml(content: string): string {
    const lines = this.stripResumeMarkers(content).replace(/\r\n/g, '\n').split('\n');
    let html = '';
    let list: 'ul' | 'ol' | '' = '';

    const closeList = (): void => {
      if (list) {
        html += `</${list}>`;
        list = '';
      }
    };

    for (const rawLine of lines) {
      const line = rawLine.trim();
      if (!line) {
        closeList();
        continue;
      }

      const heading = line.match(/^(#{1,6})\s+(.+)$/);
      if (heading) {
        closeList();
        html += heading[1].length === 1 ? `<h1>${this.inlineMarkdownToHtml(heading[2])}</h1>` : `<h2>${this.inlineMarkdownToHtml(heading[2])}</h2>`;
        continue;
      }

      const bullet = line.match(/^[-*]\s+(.+)$/);
      if (bullet) {
        if (list !== 'ul') {
          closeList();
          list = 'ul';
          html += '<ul>';
        }
        html += `<li>${this.inlineMarkdownToHtml(bullet[1])}</li>`;
        continue;
      }

      const numbered = line.match(/^(\d+[\.)])\s+(.+)$/);
      if (numbered) {
        if (list !== 'ol') {
          closeList();
          list = 'ol';
          html += '<ol>';
        }
        html += `<li>${this.inlineMarkdownToHtml(numbered[2])}</li>`;
        continue;
      }

      closeList();
      html += `<p>${this.inlineMarkdownToHtml(line)}</p>`;
    }

    closeList();
    return html;
  }

  private inlineMarkdownToHtml(value: string): string {
    return this.escapeHtml(value).replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
  }

  private editorHtmlToMarkdown(html: string): string {
    const documentFragment = new DOMParser().parseFromString(`<div>${html}</div>`, 'text/html');
    const root = documentFragment.body.firstElementChild ?? documentFragment.body;
    const lines: string[] = [];
    root.childNodes.forEach((node) => this.appendMarkdownBlock(node, lines));
    return lines.join('\n').replace(/\n{3,}/g, '\n\n').trim();
  }

  private appendMarkdownBlock(node: Node, lines: string[]): void {
    if (node.nodeType === Node.TEXT_NODE) {
      const text = node.textContent?.trim();
      if (text) {
        lines.push(text);
      }
      return;
    }

    if (!(node instanceof HTMLElement)) {
      return;
    }

    const tagName = node.tagName.toLowerCase();
    if (tagName === 'h1') {
      lines.push(`# ${this.inlineHtmlToMarkdown(node).trim()}`);
      return;
    }

    if (['h2', 'h3', 'h4', 'h5', 'h6'].includes(tagName)) {
      lines.push(`## ${this.inlineHtmlToMarkdown(node).trim()}`);
      return;
    }

    if (tagName === 'ul' || tagName === 'ol') {
      Array.from(node.children).forEach((child, index) => {
        const listType = child instanceof HTMLElement ? child.getAttribute('data-list') : null;
        const prefix = tagName === 'ol' && listType !== 'bullet' ? `${index + 1}. ` : '- ';
        lines.push(prefix + this.inlineHtmlToMarkdown(child).trim());
      });
      return;
    }

    if (tagName === 'p' || tagName === 'div') {
      const text = this.inlineHtmlToMarkdown(node).trim();
      if (text) {
        lines.push(text);
      }
    }
  }

  private inlineHtmlToMarkdown(element: Element): string {
    let result = '';
    element.childNodes.forEach((node) => {
      if (node.nodeType === Node.TEXT_NODE) {
        result += node.textContent ?? '';
        return;
      }

      if (!(node instanceof HTMLElement)) {
        return;
      }

      const tagName = node.tagName.toLowerCase();
      if (tagName === 'strong' || tagName === 'b') {
        result += `**${this.inlineHtmlToMarkdown(node).trim()}**`;
      } else if (tagName === 'br') {
        result += '\n';
      } else {
        result += this.inlineHtmlToMarkdown(node);
      }
    });

    return result.replace(/\s+/g, ' ');
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const element = this.conversationList?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }

  private loadHistory(): void {
    this.loadingHistoryList = true;
    this.historyError = '';
    this.resumeService.history().subscribe({
      next: (history) => {
        this.loadingHistoryList = false;
        this.historyLoaded = true;
        this.history = history;
        this.cd.detectChanges();
      },
      error: (error: HttpErrorResponse) => {
        this.loadingHistoryList = false;
        this.historyLoaded = true;
        this.history = [];
        this.historyError = error.error?.message ?? 'Nao foi possivel carregar o historico.';
        this.cd.detectChanges();
      }
    });
  }

  private loadUsage(): void {
    this.authService.me().subscribe({
      next: (profile) => {
        this.resumeAnalysisCount = profile.resumeAnalysisCount;
        this.resumeAnalysisLimit = profile.resumeAnalysisLimit;
        this.cd.detectChanges();
      },
      error: () => {
        this.cd.detectChanges();
      }
    });
  }

  private optimizeLoadedSessionForJob(): void {
    if (!this.sessionId || this.jobScreenshots.length === 0) {
      return;
    }

    this.error = '';
    this.analyzing = true;
    this.messages = [
      ...this.messages,
      {
        role: 'user',
        content: 'Otimize o curriculo com base nos novos prints de vaga anexados.'
      }
    ];
    this.scrollToBottom();

    this.resumeService
      .optimizeForJob(
        this.sessionId,
        this.jobScreenshots,
        this.sessionLoadedFromHistory,
        this.linkedinProfileUrl.trim(),
        this.githubProfileUrl.trim(),
        this.portfolioUrl.trim()
      )
      .subscribe({
      next: (response) => {
        this.analyzing = false;
        this.sessionId = response.sessionId || this.sessionId;
        this.sessionLoadedFromHistory = false;
        this.setOptimizedResume(response.optimizedResume);
        this.lastSavedResume = response.optimizedResume;
        this.readyToExport = response.readyToExport;
        this.atsAnalysis = this.normalizeAtsAnalysis(response.atsAnalysis ?? this.atsAnalysis);
        this.appendImprovementResponseMessages(response);
        this.jobScreenshots = [];
        this.loadHistory();
        this.cd.detectChanges();
        this.scrollToBottom();
      },
      error: (error: HttpErrorResponse) => {
        this.analyzing = false;
        this.error = this.getErrorMessage(error, 'Nao foi possivel otimizar o curriculo para esta vaga.');
        this.cd.detectChanges();
        this.scrollToBottom();
      }
    });
  }

  private getErrorMessage(error: HttpErrorResponse, fallback: string): string {
    if (error.error?.message) {
      return error.error.message;
    }

    if (typeof error.error === 'string') {
      try {
        const parsed = JSON.parse(error.error);
        if (parsed?.message) {
          return parsed.message;
        }
      } catch {
        return error.error || fallback;
      }
    }

    return error.message ? `${fallback} (${error.status || 'sem status'}: ${error.message})` : fallback;
  }

  private download(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
  }
}
