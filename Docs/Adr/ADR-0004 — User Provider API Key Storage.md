# ADR-0004 — User Provider API Key Storage

## Status

Accepted

## Contexto

O chatbot usa a API da Groq para funcionar como IA inicial da plataforma.

Na versão inicial, a API Key foi planejada para ficar apenas no `localStorage`.

Isso resolve testes rápidos, mas cria problemas:

* Usuário precisa reconfigurar a chave em outro navegador.
* Usuário perde a chave ao limpar dados locais.
* A aplicação não consegue evoluir para produto real.
* Futuramente o sistema precisará trocar de modelo de cobrança e usar chave própria da plataforma.

## Decisão

As API Keys de provedores externos serão armazenadas no banco, vinculadas ao usuário autenticado.

A chave deve ser criptografada antes de ser salva.

A chave nunca deve ser salva em texto puro.

## Escopo inicial

Provider inicial:

```text
Groq
```

Estrutura sugerida:

```text
UserProviderApiKeys
```

Campos:

```text
Id
UserId
Provider
EncryptedApiKey
CreatedAt
UpdatedAt
LastUsedAt
IsActive
```

Provider:

```text
Groq
OpenAI
Claude
Gemini
Ollama
```

Nesta fase, implementar apenas Groq.

## Regras

* Cada usuário pode ter uma chave ativa por provider.
* A chave deve ser criptografada no backend.
* O frontend nunca deve receber a chave completa depois de salva.
* O backend pode retornar apenas estado de configuração.
* Exemplo: `Groq API Key configured: true`.
* O backend deve usar a chave salva quando o chatbot for chamado.
* O usuário pode atualizar ou remover a chave.

## Endpoints sugeridos

```http
GET /api/provider-keys
POST /api/provider-keys/groq
DELETE /api/provider-keys/groq
```

### POST /api/provider-keys/groq

Entrada:

```json
{
  "apiKey": "gsk_..."
}
```

Comportamento:

* Criptografa a chave.
* Salva ou atualiza a chave do usuário.
* Não retorna a chave.

Resposta:

```json
{
  "provider": "Groq",
  "configured": true
}
```

### GET /api/provider-keys

Resposta:

```json
[
  {
    "provider": "Groq",
    "configured": true,
    "lastUsedAt": "2026-06-23T00:00:00Z"
  }
]
```

### DELETE /api/provider-keys/groq

Comportamento:

* Remove ou desativa a chave Groq do usuário.

Resposta:

```json
{
  "provider": "Groq",
  "configured": false
}
```

## Alteração no chatbot

O endpoint:

```http
POST /api/chatbot/message
```

não deve mais exigir `apiKey` no payload.

Novo payload:

```json
{
  "message": "Olá",
  "history": []
}
```

O backend deve:

1. Identificar o usuário pelo JWT.
2. Buscar a chave Groq ativa do usuário.
3. Descriptografar em memória.
4. Chamar a Groq.
5. Nunca logar a chave.
6. Atualizar `LastUsedAt`.

## Segurança

Obrigatório:

* Criptografar chave em repouso.
* Não logar chave.
* Não retornar chave ao frontend.
* Não salvar chave em `localStorage`.
* Não enviar chave a cada mensagem.
* Não colocar chave em query string.
* Não expor chave em erro.

## Migração futura

Quando o produto deixar de usar chaves individuais de usuário, a arquitetura deve permitir mudar para chave global da plataforma.

Modelo futuro:

```text
UserProvidedKey
PlatformManagedKey
```

Nesta fase, usar:

```text
UserProvidedKey
```

## Consequências

Benefícios:

* Melhor experiência do usuário.
* Preparado para produto real.
* Permite múltiplos provedores no futuro.
* Remove API Key do frontend após configuração.

Custos:

* Exige criptografia.
* Exige migration.
* Exige endpoints de gerenciamento.
* Exige cuidado com logs e erros.

## Decisão final

Persistir API Keys de provedores por usuário no banco, criptografadas, começando pela Groq.
