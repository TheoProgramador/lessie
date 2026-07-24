# EPIC-0003 — People Discovery MCP Experimental

## Objetivo

Adicionar o primeiro MCP público experimental ao projeto Lessie Clone para busca de pessoas.

A IA Groq já configurada no chatbot será usada como orquestradora inicial.

A API Key da Groq configurada no chatbot deve ser reutilizada para esta funcionalidade.

---

## Decisão principal

A chave Groq será configurada uma única vez na tela do chatbot e armazenada no `localStorage`.

A mesma chave será usada para:

* Conversa normal do chatbot.
* Orquestração inicial de ferramentas.
* Decisão de quando acionar People Discovery.

Não criar segunda configuração de API Key.

Não criar tela separada de configuração Groq nesta etapa.

---

## Escopo

Implementar:

* Primeiro contrato interno de ferramenta.
* Tool Registry simples.
* People Discovery Tool experimental.
* Adapter para MCP público de busca de pessoas.
* Integração da ferramenta com o chatbot.
* Uso da mesma API Key Groq já salva no frontend.
* Exibição do resultado da busca dentro do chatbot.

---

## Fora do escopo

Não implementar agora:

* Persistência de resultados no banco.
* CRM.
* Busca de empresas.
* Busca de vagas.
* Enriquecimento avançado.
* Envio de e-mails.
* Automação de contato.
* Scraping em massa.
* Multi-MCP.
* Agente autônomo.
* Agendamento.
* Execução sem confirmação do usuário.

---

## Regra de segurança

A IA não deve chamar MCP público diretamente.

Fluxo obrigatório:

```text
Angular Chatbot
  -> Backend Chatbot Endpoint
  -> Groq Orchestrator
  -> Internal Tool Decision
  -> Tool Registry
  -> People Discovery Tool
  -> MCP Adapter
  -> Public MCP
  -> Backend
  -> Angular Chatbot
```

Não implementar:

```text
Chatbot
  -> MCP público direto
```

---

## API Key Groq

A API Key continua armazenada no frontend em:

```text
localStorage["groqApiKey"]
```

O frontend deve enviar a API Key ao backend nas chamadas do chatbot, como já definido no EPIC-0002.

O backend não deve persistir a API Key.

O backend não deve logar a API Key.

O backend não deve retornar a API Key.

---

## Comportamento esperado

Quando o usuário pedir algo como:

```text
Encontre recrutadores .NET no Brasil
```

ou:

```text
Procure pessoas relacionadas a Angular e AWS
```

a IA deve reconhecer intenção de busca de pessoas.

Nesta fase experimental, existem duas opções aceitáveis:

### Opção A — Detecção simples por backend

O backend identifica termos como:

* encontrar pessoas
* procurar pessoas
* buscar recrutadores
* buscar profissionais
* pessoas relacionadas a

E chama a People Discovery Tool.

### Opção B — Detecção via Groq

O backend envia a mensagem para a Groq pedindo uma decisão estruturada:

```json
{
  "action": "people.search",
  "query": "recrutadores .NET Brasil"
}
```

Para esta etapa, priorizar a solução mais simples e funcional.

Se a detecção via Groq gerar complexidade excessiva, usar detecção simples por backend.

---

## Contrato interno de ferramenta

Criar um contrato simples:

```csharp
public interface ITool
{
    string Name { get; }

    Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        CancellationToken cancellationToken);
}
```

DTOs sugeridos:

```csharp
public sealed class ToolRequest
{
    public string Query { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
```

```csharp
public sealed class ToolResult
{
    public bool Success { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public object? Data { get; set; }
    public string? Error { get; set; }
}
```

---

## Tool Registry

Criar um registry simples.

Responsabilidades:

* Registrar ferramentas disponíveis.
* Localizar ferramenta por nome.
* Executar ferramenta solicitada.

Ferramenta inicial:

```text
people.search
```

Não criar sistema avançado de permissões agora.

---

## People Discovery Tool

Nome:

```text
people.search
```

Responsabilidade:

* Receber uma query de busca de pessoas.
* Chamar adapter MCP experimental.
* Retornar dados estruturados.

Entrada:

```json
{
  "query": "recrutadores .NET Brasil"
}
```

Saída esperada:

```json
{
  "success": true,
  "toolName": "people.search",
  "summary": "Foram encontrados resultados relacionados à busca.",
  "data": [
    {
      "name": "...",
      "title": "...",
      "company": "...",
      "location": "...",
      "profileUrl": "..."
    }
  ]
}
```

---

## MCP público experimental

Implementar como adapter isolado.

Nome sugerido:

```text
LinkedInPeopleMcpAdapter
```

Regra:

* O adapter deve ficar isolado da lógica principal.
* Se o MCP falhar, o chatbot deve continuar funcionando.
* Se o MCP não estiver configurado, retornar erro amigável.

Não espalhar dependências do MCP pelo sistema.

---

## Configuração do MCP

Nesta fase, aceitar configuração simples via arquivo de configuração do backend.

Exemplo:

```json
{
  "Mcp": {
    "PeopleDiscovery": {
      "Enabled": true,
      "Provider": "LinkedInPublicMcp",
      "Command": "node",
      "Arguments": ["path/to/linkedin-mcp-server"]
    }
  }
}
```

Se o projeto ainda não tiver MCP runtime pronto, implementar um adapter mockado temporário com a mesma interface.

O importante é preservar o contrato `people.search`.

---

## Integração com o chatbot

Atualizar o endpoint existente:

```http
POST /api/chatbot/message
```

O endpoint deve:

1. Receber mensagem, histórico e Groq API Key.
2. Detectar se a mensagem é busca de pessoas.
3. Se for busca de pessoas, executar `people.search`.
4. Enviar o resultado para Groq gerar uma resposta amigável.
5. Retornar a resposta para o frontend.

---

## Payload atual preservado

Manter compatibilidade com o payload do EPIC-0002:

```json
{
  "apiKey": "groq_api_key_here",
  "message": "Encontre recrutadores .NET no Brasil",
  "history": []
}
```

Não criar novo endpoint obrigatório nesta fase.

---

## Resposta esperada

```json
{
  "message": "Encontrei alguns perfis relacionados..."
}
```

Opcionalmente, se já existir estrutura no frontend, retornar também:

```json
{
  "message": "...",
  "toolResult": {
    "toolName": "people.search",
    "data": []
  }
}
```

Prioridade: manter o chatbot funcionando.

---

## Frontend

No frontend:

* Não criar nova tela.
* Usar a tela atual do chatbot.
* Usar a mesma API Key Groq já salva.
* Mostrar a resposta da IA normalmente.
* Se `toolResult` vier na resposta, exibir opcionalmente um bloco simples com resultados.

Não criar interface avançada de leads ainda.

---

## Critérios de aceite

O épico estará pronto quando:

* A tela do chatbot continua funcionando.
* A API Key Groq configurada no chatbot é reutilizada.
* O usuário consegue pedir busca de pessoas pelo chat.
* O backend detecta a intenção de busca de pessoas.
* O backend executa `people.search`.
* A resposta final é gerada pela Groq.
* Falha no MCP não quebra o chatbot.
* Nenhuma chave Groq é salva no banco.
* Nenhuma chave Groq aparece em log.
* Nenhum MCP é chamado diretamente pelo frontend.

---

## Ordem de implementação

Executar nesta ordem:

```text
01 - Revisar implementação atual do chatbot
02 - Criar contrato interno ITool
03 - Criar ToolRequest e ToolResult
04 - Criar ToolRegistry simples
05 - Criar PeopleDiscoveryTool
06 - Criar adapter MCP experimental ou mock temporário
07 - Atualizar Chatbot endpoint para detectar busca de pessoas
08 - Executar people.search quando aplicável
09 - Enviar resultado da ferramenta para Groq gerar resposta
10 - Exibir resposta no chatbot
11 - Tratar falhas sem quebrar o chat
```

---

## Restrições para o Codex

Antes de alterar código:

1. Verificar padrões existentes.
2. Não quebrar EPIC-0002.
3. Não alterar login.
4. Não criar nova configuração Groq.
5. Não salvar API Key no banco.
6. Não criar nova tela.
7. Não implementar CRM.
8. Não implementar empresas ou vagas ainda.
9. Não acoplar MCP público diretamente ao chatbot.
10. Não criar arquitetura genérica infinita.

---

## Definition of Done

Concluído apenas quando for possível:

1. Logar.
2. Abrir chatbot.
3. Usar a API Key Groq já configurada.
4. Enviar mensagem normal e receber resposta normal.
5. Enviar pedido de busca de pessoas.
6. Ver execução de `people.search`.
7. Receber resposta da IA baseada no resultado da ferramenta.
8. Confirmar que falha do MCP não derruba o chatbot.

---

## Princípio final

Este épico existe para provar a primeira integração de descoberta de pessoas.

A IA Groq continua sendo a orquestradora inicial.

O MCP é experimental e deve ficar isolado.

O sistema deve continuar simples, funcional e substituível.
