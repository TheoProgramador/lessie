# EPIC-0002 — Chatbot Groq Orchestrator Foundation

## Objetivo

Implementar uma primeira versão funcional do chatbot dentro da aplicação Lessie Clone.

Este chatbot será a primeira interface da IA que futuramente irá orquestrar ferramentas e MCPs.

Nesta fase, o chatbot deve apenas conversar usando a API da Groq.

Não implementar MCP real agora.

---

## Escopo

Implementar:

* Item de menu no frontend para acessar o chatbot.
* Tela completa de chatbot.
* Armazenamento local da API Key no frontend.
* Endpoint backend para enviar mensagens à Groq.
* Integração funcional com modelo da Groq.
* Histórico visual da conversa na tela.
* Loading durante resposta da IA.
* Tratamento básico de erro.

---

## Fora do escopo

Não implementar agora:

* MCP real.
* Busca de pessoas.
* Busca de empresas.
* Busca de vagas.
* Persistência de conversas no banco.
* Streaming.
* Multi-model provider.
* RAG.
* Upload de arquivos.
* Agentes complexos.
* Tool calling real.

O objetivo desta etapa é fazer o chatbot funcionar.

---

## Decisão técnica

A IA usada nesta fase será Groq.

O backend deve chamar a API da Groq.

O frontend não deve chamar a Groq diretamente.

Fluxo:

```text
Angular Chatbot
  -> Backend Lessie API
  -> Groq API
  -> Backend Lessie API
  -> Angular Chatbot
```

---

## Modelo sugerido

Usar inicialmente:

```text
openai/gpt-oss-120b
```

Caso o modelo não esteja disponível na conta Groq local, deixar o nome do modelo configurável no backend.

---

## Frontend

### Menu

Adicionar um item no menu lateral do Datta Able:

```text
Chatbot
```

Rota sugerida:

```text
/chatbot
```

O item deve aparecer apenas para usuário autenticado.

---

## Tela do Chatbot

Criar uma tela completa com:

### Área superior

Título:

```text
AI Orchestrator Chatbot
```

Subtítulo:

```text
Primeira versão da IA que futuramente irá orquestrar MCPs e ferramentas.
```

---

### Área de configuração

Criar um card de configuração com:

* Campo para API Key da Groq.
* Botão "Salvar API Key".
* Botão "Limpar API Key".

A API Key deve ser salva no `localStorage`.

Nome da chave no localStorage:

```text
groqApiKey
```

Não enviar API Key para banco.

Persistir API Key no backend.

---

### Área de chat

Criar layout contendo:

* Lista de mensagens.
* Mensagens do usuário alinhadas à direita.
* Mensagens da IA alinhadas à esquerda.
* Campo de texto para digitar mensagem.
* Botão enviar.
* Estado de carregamento enquanto aguarda resposta.
* Tratamento visual de erro.

---

## Frontend Services

Criar ou ajustar:

```text
ChatbotService
GroqSettingsService
```

### GroqSettingsService

Responsável por:

* Salvar API Key no localStorage.
* Ler API Key do localStorage.
* Remover API Key do localStorage.
* Informar se existe API Key configurada.

### ChatbotService

Responsável por chamar o backend:

```http
POST /api/chatbot/message
```

Payload:

```json
{
  "apiKey": "groq_api_key_here",
  "message": "mensagem do usuário",
  "history": [
    {
      "role": "user",
      "content": "mensagem anterior"
    },
    {
      "role": "assistant",
      "content": "resposta anterior"
    }
  ]
}
```

Resposta esperada:

```json
{
  "message": "resposta da IA"
}
```

---

## Backend

Criar endpoint:

```http
POST /api/chatbot/message
```

Endpoint deve exigir autenticação JWT.

---

## Request DTO

```csharp
public sealed class ChatbotMessageRequest
{
    public string ApiKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageDto> History { get; set; } = new();
}
```

```csharp
public sealed class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
```

---

## Response DTO

```csharp
public sealed class ChatbotMessageResponse
{
    public string Message { get; set; } = string.Empty;
}
```

---

## Validações

O backend deve validar:

* API Key obrigatória.
* Mensagem obrigatória.
* Histórico opcional.
* Roles permitidas no histórico:

  * `user`
  * `assistant`
  * `system`

Se inválido, retornar `400`.

Se Groq retornar erro de autenticação, retornar `401` ou `400` com mensagem clara.

Se Groq falhar por outro motivo, retornar `502`.

---

## System Prompt

Usar um system prompt simples:

```text
You are the first AI orchestrator of Lessie Clone.

For now, you only answer as a normal chatbot.

In the future, you will orchestrate MCP tools for people discovery, company discovery, opportunity discovery and lead enrichment.

Be concise, useful and practical.
```

Não implementar tool calling agora.

---

## Groq API

O backend deve chamar a API da Groq usando HTTP.

Endpoint Groq:

```text
https://api.groq.com/openai/v1/chat/completions
```

Headers:

```text
Authorization: Bearer {apiKey}
Content-Type: application/json
```

Payload sugerido:

```json
{
  "model": "openai/gpt-oss-120b",
  "messages": [
    {
      "role": "system",
      "content": "You are the first AI orchestrator of Lessie Clone..."
    },
    {
      "role": "user",
      "content": "mensagem"
    }
  ],
  "temperature": 0.3
}
```

O histórico recebido do frontend deve ser incluído entre o system prompt e a nova mensagem do usuário.

---

## Segurança

Nesta fase, a API Key será armazenada no `localStorage`.

Isso é aceitável apenas para desenvolvimento e MVP local.

Não logar API Key.

Não salvar API Key no banco.

Não retornar API Key em respostas.

Não exibir API Key completa depois de salva.

Na interface, mostrar apenas indicação de que a chave está configurada.

Exemplo:

```text
Groq API Key configurada.
```

---

## Critérios de aceite

O épico estará pronto quando:

* Usuário autenticado vê item "Chatbot" no menu.
* Usuário acessa `/chatbot`.
* Tela de chatbot renderiza corretamente.
* Usuário consegue salvar API Key da Groq.
* Usuário consegue limpar API Key da Groq.
* Usuário envia uma mensagem.
* Backend chama Groq.
* Resposta da IA aparece na tela.
* Erros são exibidos de forma compreensível.
* Endpoint `/api/chatbot/message` exige JWT.
* API Key não é salva no banco.
* API Key não aparece em logs.
* Nenhum MCP real foi implementado.

---

## Ordem de implementação

Executar nesta ordem:

```text
01 - Criar rota /chatbot no Angular
02 - Adicionar item de menu Chatbot no Datta Able
03 - Criar tela Chatbot
04 - Criar GroqSettingsService
05 - Criar ChatbotService
06 - Criar DTOs no backend
07 - Criar endpoint POST /api/chatbot/message
08 - Implementar Groq client no backend
09 - Conectar frontend ao backend
10 - Tratar loading e erros
11 - Testar fluxo completo
```

---

## Restrições para o Codex

Antes de implementar:

1. Verificar padrões existentes no frontend.
2. Verificar padrões existentes no backend.
3. Não alterar autenticação existente.
4. Não quebrar login Google.
5. Não criar banco novo.
6. Não criar tabela de conversa ainda.
7. Não implementar MCP.
8. Não implementar streaming.
9. Não criar arquitetura genérica excessiva.
10. Não hardcodar API Key.

---

## Definition of Done

Considerar concluído apenas quando for possível:

1. Logar no sistema.
2. Abrir o menu Chatbot.
3. Configurar API Key Groq.
4. Enviar mensagem.
5. Receber resposta real da Groq.
6. Recarregar a página e manter API Key no localStorage.
7. Limpar API Key.
8. Confirmar que o backend exige JWT no endpoint do chatbot.

---

## Princípio final

Este épico existe para estabilizar a IA base antes das integrações MCP.

Não transformar esta etapa em produto final.

Não transformar esta etapa em arquitetura abstrata infinita.

Fazer funcionar.
