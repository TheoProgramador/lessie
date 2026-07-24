import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ChatbotService, ChatMessage } from 'src/app/services/chatbot.service';
import { AuthService } from 'src/app/services/auth.service';
import { GroqSettingsService } from 'src/app/services/groq-settings.service';
import { SharedModule } from 'src/app/theme/shared/shared.module';

@Component({
  selector: 'app-chatbot',
  imports: [CommonModule, FormsModule, SharedModule],
  templateUrl: './chatbot.component.html',
  styleUrls: ['./chatbot.component.scss']
})
export class ChatbotComponent implements OnInit {
  @ViewChild('messageList') private messageList?: ElementRef<HTMLDivElement>;
  @ViewChild('pollinationsMessageList') private pollinationsMessageList?: ElementRef<HTMLDivElement>;

  private readonly chatbotService = inject(ChatbotService);
  private readonly groqSettingsService = inject(GroqSettingsService);
  private readonly authService = inject(AuthService);
  private readonly cd = inject(ChangeDetectorRef);

  activeChat: 'groq' | 'pollinations' = 'groq';
  apiKeyInput = '';
  pollinationsTokenInput = '';
  messageInput = '';
  pollinationsMessageInput = '';
  error = '';
  pollinationsError = '';
  success = '';
  pollinationsSuccess = '';
  loading = false;
  pollinationsLoading = false;
  settingsLoading = false;
  usageLoading = false;
  hasApiKey = this.groqSettingsService.hasApiKey();
  hasPollinationsToken = this.groqSettingsService.hasPollinationsToken();
  messages: ChatMessage[] = [];
  pollinationsMessages: ChatMessage[] = [];
  chatConversationCount = 0;
  chatConversationLimit = 50;

  ngOnInit(): void {
    this.loadProviderStatus();
    this.loadUsage();
  }

  loadProviderStatus(): void {
    this.settingsLoading = true;
    this.groqSettingsService.loadStatus().subscribe({
      next: () => {
        this.hasApiKey = this.groqSettingsService.hasApiKey();
        this.hasPollinationsToken = this.groqSettingsService.hasPollinationsToken();
        this.settingsLoading = false;
        this.cd.detectChanges();
      },
      error: () => {
        this.settingsLoading = false;
        this.error = 'Nao foi possivel carregar a configuracao da Groq.';
        this.cd.detectChanges();
      }
    });
  }

  loadUsage(): void {
    this.usageLoading = true;
    this.authService.me().subscribe({
      next: (profile) => {
        this.chatConversationCount = profile.chatConversationCount;
        this.chatConversationLimit = profile.chatConversationLimit;
        this.usageLoading = false;
        this.cd.detectChanges();
      },
      error: () => {
        this.usageLoading = false;
        this.cd.detectChanges();
      }
    });
  }

  saveApiKey(): void {
    const apiKey = this.apiKeyInput.trim();
    this.error = '';
    this.success = '';

    if (!apiKey) {
      this.error = 'Informe a API Key da Groq.';
      return;
    }

    this.settingsLoading = true;
    this.groqSettingsService.saveApiKey(apiKey).subscribe({
      next: () => {
        this.apiKeyInput = '';
        this.hasApiKey = true;
        this.settingsLoading = false;
        this.success = 'Groq API Key configurada.';
        this.cd.detectChanges();
      },
      error: (error: HttpErrorResponse) => {
        this.settingsLoading = false;
        this.error = error.error?.message ?? 'Nao foi possivel salvar a API Key da Groq.';
        this.cd.detectChanges();
      }
    });
  }

  clearApiKey(): void {
    this.settingsLoading = true;
    this.groqSettingsService.clearApiKey().subscribe({
      next: () => {
        this.apiKeyInput = '';
        this.hasApiKey = false;
        this.settingsLoading = false;
        this.success = '';
        this.error = '';
        this.cd.detectChanges();
      },
      error: (error: HttpErrorResponse) => {
        this.settingsLoading = false;
        this.error = error.error?.message ?? 'Nao foi possivel limpar a API Key da Groq.';
        this.cd.detectChanges();
      }
    });
  }

  savePollinationsToken(): void {
    const token = this.pollinationsTokenInput.trim();
    this.pollinationsError = '';
    this.pollinationsSuccess = '';

    if (!token) {
      this.pollinationsError = 'Informe o token da Pollinations.';
      return;
    }

    this.settingsLoading = true;
    this.groqSettingsService.savePollinationsToken(token).subscribe({
      next: () => {
        this.pollinationsTokenInput = '';
        this.hasPollinationsToken = true;
        this.settingsLoading = false;
        this.pollinationsSuccess = 'Token Pollinations configurado.';
        this.cd.detectChanges();
      },
      error: (error: HttpErrorResponse) => {
        this.settingsLoading = false;
        this.pollinationsError = error.error?.message ?? 'Nao foi possivel salvar o token da Pollinations.';
        this.cd.detectChanges();
      }
    });
  }

  clearPollinationsToken(): void {
    this.settingsLoading = true;
    this.groqSettingsService.clearPollinationsToken().subscribe({
      next: () => {
        this.pollinationsTokenInput = '';
        this.hasPollinationsToken = false;
        this.settingsLoading = false;
        this.pollinationsSuccess = '';
        this.pollinationsError = '';
        this.cd.detectChanges();
      },
      error: (error: HttpErrorResponse) => {
        this.settingsLoading = false;
        this.pollinationsError = error.error?.message ?? 'Nao foi possivel limpar o token da Pollinations.';
        this.cd.detectChanges();
      }
    });
  }

  selectChat(chat: 'groq' | 'pollinations'): void {
    this.activeChat = chat;
  }

  sendMessage(): void {
    const message = this.messageInput.trim();
    this.error = '';
    this.success = '';

    if (!this.hasApiKey) {
      this.error = 'Configure a API Key da Groq antes de enviar mensagens.';
      return;
    }

    if (!message || this.loading) {
      return;
    }

    const history = [...this.messages];
    this.messages = [...this.messages, { role: 'user', content: message }];
    this.messageInput = '';
    this.loading = true;
    this.scrollToBottom();

    this.chatbotService
      .sendMessage({
        message,
        history
      })
      .subscribe({
        next: (response) => {
          this.messages = [...this.messages, { role: 'assistant', content: response.message, toolResult: response.toolResult }];
          this.loading = false;
          this.cd.detectChanges();
          this.scrollToBottom();
        },
        error: (error: HttpErrorResponse) => {
          this.loading = false;
          this.error = error.error?.message ?? 'Nao foi possivel obter resposta da Groq.';
          this.cd.detectChanges();
          this.scrollToBottom();
        }
      });
  }

  sendPollinationsMessage(): void {
    const message = this.pollinationsMessageInput.trim();
    this.pollinationsError = '';
    this.pollinationsSuccess = '';

    if (!this.hasPollinationsToken) {
      this.pollinationsError = 'Configure o token da Pollinations antes de enviar mensagens.';
      return;
    }

    if (!message || this.pollinationsLoading) {
      return;
    }

    const history = [...this.pollinationsMessages];
    this.pollinationsMessages = [...this.pollinationsMessages, { role: 'user', content: message }];
    this.pollinationsMessageInput = '';
    this.pollinationsLoading = true;
    this.scrollPollinationsToBottom();

    this.chatbotService
      .sendPollinationsMessage({
        message,
        history
      })
      .subscribe({
        next: (response) => {
          this.pollinationsMessages = [...this.pollinationsMessages, { role: 'assistant', content: response.message }];
          this.pollinationsLoading = false;
          this.loadUsage();
          this.cd.detectChanges();
          this.scrollPollinationsToBottom();
        },
        error: (error: HttpErrorResponse) => {
          this.pollinationsLoading = false;
          this.pollinationsError = error.error?.message ?? 'Nao foi possivel obter resposta da Pollinations.';
          this.cd.detectChanges();
          this.scrollPollinationsToBottom();
        }
      });
  }

  onComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  onPollinationsComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendPollinationsMessage();
    }
  }

  trackMessage(index: number): number {
    return index;
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const element = this.messageList?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }

  private scrollPollinationsToBottom(): void {
    setTimeout(() => {
      const element = this.pollinationsMessageList?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }
}
