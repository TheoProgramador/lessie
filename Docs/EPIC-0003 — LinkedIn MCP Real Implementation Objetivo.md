# EPIC-0003 — LinkedIn MCP Real Implementation

## Objetivo

Substituir o mock atual de People Discovery por uma integração real com um MCP público de LinkedIn.

A tela `People Discovery` deve executar uma busca real, retornar dados reais do MCP e exibir esses dados no frontend.

Mocks não são aceitos nesta entrega.

---

## Problema Atual

A implementação atual de `LinkedInPeopleMcpAdapter` retorna dados fixos:

```csharp
Ana Souza
Bruno Lima
Carla Mendes
```

Isso não é MCP.

Isso não valida integração.

Isso não prova funcionalidade.

Isso deve ser removido.

---

## MCP escolhido

Usar como primeira opção:

```text
eliasbiondo/linkedin-mcp-server
```

Motivo:

* Suporte a busca de pessoas.
* Suporte a empresas.
* Suporte a jobs.
* Retorno estruturado em JSON.
* Alinhado ao objetivo do produto.

Nesta etapa, implementar apenas busca de pessoas.

---

## Regra Principal

Não retornar dados mockados.

Se o MCP não estiver instalado, configurado ou executando, o backend deve retornar erro claro.

Exemplo:

```json
{
  "success": false,
  "source": "mcp",
  "toolName": "people.search",
  "summary": "LinkedIn MCP is not available.",
  "results": [],
  "error": "LinkedIn MCP process could not be started or did not return a valid response."
}
```

---

## Tela

Manter a tela real do produto:

```text
/people-discovery
```

Não criar tela de laboratório.

Não criar tela temporária.

A tela deve conter:

* Campo de busca.
* Botão de busca.
* Loading.
* Estado vazio.
* Estado de erro.
* Lista/tabela/cards com resultados.

---

## Endpoint

Criar ou ajustar:

```http
POST /api/people-discovery/search
```

Requer JWT.

Payload:

```json
{
  "query": ".NET recruiters Brazil remote"
}
```

Resposta esperada:

```json
{
  "success": true,
  "source": "mcp",
  "toolName": "people.search",
  "summary": "Results found.",
  "results": [
    {
      "name": "Person name",
      "title": "Professional title",
      "company": "Company name",
      "location": "Location",
      "profileUrl": "https://www.linkedin.com/in/...",
      "source": "LinkedIn"
    }
  ],
  "error": null
}
```

---

## Backend

A interface existente pode ser mantida:

```csharp
public interface IPeopleDiscoveryAdapter
{
    Task<IReadOnlyCollection<PeopleDiscoveryPersonDto>> SearchAsync(
        string query,
        CancellationToken cancellationToken);
}
```

Mas a implementação `LinkedInPeopleMcpAdapter` deve executar MCP real.

---

## Remover Mock

Remover completamente qualquer retorno fixo como:

```csharp
new("Ana Souza", ...)
new("Bruno Lima", ...)
new("Carla Mendes", ...)
```

Esse código deve deixar de existir.

Se for necessário manter fallback para desenvolvimento, ele deve ser outro adapter explícito:

```text
MockPeopleDiscoveryAdapter
```

E só pode ser ativado quando:

```json
{
  "Mcp": {
    "PeopleDiscovery": {
      "Provider": "Mock"
    }
  }
}
```

Para esta entrega, o provider padrão deve ser:

```text
LinkedInMcp
```

---

## Configuração

Adicionar configuração no backend:

```json
{
  "Mcp": {
    "PeopleDiscovery": {
      "Enabled": true,
      "Provider": "LinkedInMcp",
      "Command": "python",
      "Arguments": [
        "-m",
        "linkedin_mcp_server"
      ],
      "WorkingDirectory": "external/linkedin-mcp-server",
      "TimeoutSeconds": 60
    }
  }
}
```

A configuração real deve ser ajustada conforme a forma de execução do MCP instalado no projeto.

Não hardcodar caminho no adapter.

Usar `IConfiguration`.

---

## Pasta de integrações externas

Criar pasta para integrações externas, se ainda não existir:

```text
external/
  linkedin-mcp-server/
```

O projeto deve conter documentação explicando como instalar ou clonar o MCP.

Não copiar código do MCP para dentro do backend.

Não misturar código externo com código da aplicação.

---

## Execução do MCP

O backend deve executar o MCP usando processo configurável.

Responsabilidades do adapter:

1. Ler configuração.
2. Validar se está habilitado.
3. Iniciar processo MCP quando necessário.
4. Enviar chamada para ferramenta de busca de pessoas.
5. Ler resposta.
6. Converter resposta para `PeopleDiscoveryPersonDto`.
7. Retornar resultado estruturado.
8. Encerrar processo ou reutilizar conforme implementação simples e segura.

---

## Ferramenta MCP esperada

A ferramenta esperada deve ser equivalente a:

```text
search_people
```

Entrada conceitual:

```json
{
  "query": ".NET recruiters Brazil remote"
}
```

A implementação deve verificar o nome real da tool exposta pelo MCP instalado.

Não inventar nome de tool sem inspecionar o servidor MCP.

Antes de codar fixo, consultar o README do MCP escolhido.

---

## Contrato de saída interno

Normalizar qualquer retorno do MCP para:

```csharp
public sealed class PeopleDiscoveryPersonDto
{
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string ProfileUrl { get; init; } = string.Empty;
    public string Source { get; init; } = "LinkedIn";
}
```

Se algum campo não vier do MCP, retornar string vazia.

Não quebrar a tela por campo ausente.

---

## Tratamento de erro

Se o MCP não estiver configurado:

```text
LinkedIn MCP is not configured.
```

Se o processo não iniciar:

```text
LinkedIn MCP process could not be started.
```

Se a resposta vier inválida:

```text
LinkedIn MCP returned an invalid response.
```

Se não houver resultados:

```text
No people found for this search.
```

Não retornar dados falsos.

---

## Frontend

A tela `People Discovery` deve chamar:

```http
POST /api/people-discovery/search
```

E exibir os dados úteis.

Cada resultado deve mostrar:

* Nome.
* Cargo.
* Empresa.
* Localização.
* Origem.
* Link do perfil.

Se `profileUrl` existir, abrir em nova aba.

---

## Critérios de aceite

Concluído apenas quando:

* O mock atual foi removido.
* Existe configuração para executar LinkedIn MCP real.
* Backend chama MCP real.
* Endpoint `/api/people-discovery/search` retorna dados vindos do MCP.
* Tela `/people-discovery` exibe os dados retornados.
* Se o MCP estiver indisponível, aparece erro claro.
* Nenhum dado fixo é retornado fingindo ser MCP.
* Chatbot continua funcionando.
* Login continua funcionando.

---

## Teste manual obrigatório

Executar:

```text
.NET recruiters Brazil remote
```

Resultado esperado:

* A tela mostra loading.
* Backend chama o MCP.
* A tela exibe resultados reais ou erro claro do MCP.
* Não aparecem nomes mockados.

---

## Restrições para o Codex

Não fazer:

* Não retornar dados fixos.
* Não criar mock como provider padrão.
* Não esconder erro de MCP retornando exemplo fake.
* Não criar dados de demonstração.
* Não alterar login.
* Não quebrar chatbot.
* Não salvar resultados no banco ainda.
* Não implementar empresas.
* Não implementar jobs.
* Não implementar CRM.

---

## Definition of Done

A entrega só está pronta quando o usuário consegue buscar pessoas na tela `People Discovery` e ver resultado real vindo do LinkedIn MCP ou erro real explicando por que o MCP não executou.

Qualquer retorno fixo conta como falha da entrega.
