# EPIC-0003 ADDENDUM — People Discovery Product UI

## Objetivo

Criar a primeira tela funcional e permanente de People Discovery.

Esta tela não é apenas laboratório. Ela será a base real da funcionalidade de busca de pessoas do produto.

## Escopo

Implementar:

* Item de menu no padrão do Datta Able.
* Tela `People Discovery`.
* Campo de busca no padrão visual do layout.
* Botão de busca.
* Área de resultados útil.
* Loading.
* Erro.
* Estado vazio.
* Exibição clara dos dados retornados pelo MCP.
* Estrutura visual reaproveitável para evolução futura.

## Menu

Adicionar item autenticado:

```text
People Discovery
```

Rota:

```text
/people-discovery
```

Não usar nome de laboratório, teste ou experimental na interface.

## Tela

Título:

```text
People Discovery
```

Subtítulo:

```text
Find professionals, recruiters and relevant contacts using connected discovery tools.
```

## Campo de busca

Criar campo no padrão do template, com placeholder:

```text
Search for recruiters, developers, companies or roles...
```

Exemplos de busca:

```text
.NET recruiters Brazil remote
Angular developers São Paulo
AWS recruiters LATAM
```

## Endpoint

Criar ou ajustar endpoint:

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

Resposta:

```json
{
  "success": true,
  "source": "mock | adapter | mcp",
  "summary": "Results found for the requested search.",
  "results": [
    {
      "name": "Name",
      "title": "Title",
      "company": "Company",
      "location": "Location",
      "profileUrl": "https://...",
      "source": "LinkedIn"
    }
  ],
  "error": null
}
```

## Regras do backend

* Reutilizar a tool `people.search`.
* Não depender do chatbot.
* Não depender da Groq.
* Validar query obrigatória.
* Retornar dados estruturados.
* Em falha, retornar erro controlado.
* Não quebrar a tela se o MCP falhar.
* Não criar persistência ainda.

## Regras do frontend

A tela deve exibir os resultados em formato útil, preferencialmente cards ou tabela responsiva.

Cada resultado deve mostrar:

* Nome
* Cargo
* Empresa
* Localização
* Origem
* Link de perfil, quando existir

Se não houver resultados:

```text
No people found for this search.
```

Se houver erro:

```text
Unable to run People Discovery.
```

Se estiver carregando:

```text
Searching people...
```

## Critérios de aceite

Concluído quando:

* Existe menu `People Discovery`.
* Existe rota `/people-discovery`.
* Tela segue o padrão visual do Datta Able.
* Usuário autenticado consegue buscar pessoas.
* Resultado aparece na tela.
* Dados são úteis, não apenas JSON cru.
* Loading funciona.
* Erro funciona.
* Estado vazio funciona.
* Chatbot continua funcionando.
* Login continua funcionando.

## Fora do escopo

Não implementar agora:

* Salvar lead.
* Favoritar pessoa.
* Exportar.
* Enviar mensagem.
* Buscar empresas.
* Buscar vagas.
* Enriquecimento avançado.
* Paginação.
* Filtros avançados.

## Restrição importante

Não criar tela com cara de teste descartável.

Esta tela deve ser simples, mas já deve nascer como parte real do produto.
