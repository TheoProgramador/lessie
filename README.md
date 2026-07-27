# Lessie

Lessie é uma plataforma para apoiar busca de vagas, prospecção profissional e evolução de currículo usando autenticação Google, IA, integrações externas, créditos pagos e painel web em Angular.

O repositório está organizado para apoiar desenvolvimento moderno, colaboração humana e trabalho assistido por IA, mantendo a estrutura do produto e a integridade das regras de negócio.

## Visão geral

- `Backend`: API .NET com SQL Server, autenticação, pagamentos, IA e persistência.
- `FrontEnd`: aplicação Angular com experiência web para usuários autenticados e administradores.
- `Docs`: documentação de arquitetura, roadmap e épicos do produto.
- `external`: integrações e projetos auxiliares, incluindo MCPs e componentes experimentais.

## Desenvolvimento assistido por IA

Este repositório foi preparado para facilitar o trabalho com ferramentas de IA, como GitHub Copilot e agentes de execução. As boas práticas recomendadas são:

- ler a documentação e a estrutura existente antes de editar;
- preservar contratos entre backend e frontend;
- evitar incluir segredos, tokens ou credenciais em arquivos rastreados;
- validar mudanças com os comandos de build e execução mais próximos do escopo da alteração.

## Funcionalidades

- Login com Google via Google Identity Services.
- Autenticacao propria com JWT, refresh token, logout e endpoint `/api/me`.
- Login de desenvolvimento para admin, limitado ao ambiente local.
- Perfil administrativo para `theo.miliani@gmail.com`.
- Rotas protegidas por autenticacao, assinatura/creditos e permissao de admin.
- Landing page publica com compra de creditos.
- Area autenticada com dashboard, creditos, curriculo, oportunidades, pessoas, entrevistas e chat.
- Gate de pagamento para bloquear funcionalidades pagas quando o usuario nao possui acesso.
- Planos de creditos configuraveis no banco de dados.
- Promocoes configuraveis no banco de dados, com cupom, desconto, bonus de creditos, periodo de validade e limite de uso.
- Integracao Mercado Pago Checkout Pro para criacao de preferencias de pagamento.
- Webhook Mercado Pago em `/api/payments/mercado-pago/webhook`.
- Registro de pedidos de pagamento e liberacao de creditos quando o pagamento aprovado e processado.
- Chaves de provedores por usuario, salvas criptografadas no banco.
- Chatbot com Groq e Pollinations, usando API key do usuario quando necessario.
- Pagina de chat liberada somente para admin.
- Busca de pessoas, publicacoes e vagas por integracoes MCP e ferramentas externas.
- Busca de oportunidades por JobSpy, Jobscope, JdIntel e JobsearchBuddy.
- Salvamento de textos de busca e resultados encontrados por usuario.
- Marcacao de curriculo enviado em resultados de prospeccao.
- Melhoria de curriculo com analise ATS, contexto externo e historico de sessoes.
- Analise de entrevista com audio/texto e resposta por IA.
- CORS preparado para frontend local, rede local e origens configuradas.
- Inicializacao automatica do schema de desenvolvimento quando a API roda em `Development`.

## Arquitetura

### Backend

O backend fica em `Backend/src` e segue uma divisao por camadas:

- `Api`: controllers, middlewares, configuracao HTTP, CORS, auth e OpenAPI.
- `Application`: contratos, DTOs e interfaces de servicos.
- `Domain`: entidades do dominio.
- `Infrastructure`: Entity Framework, SQL Server, Mercado Pago, Google, JWT, Groq, Pollinations e adapters externos.

Controllers principais:

- `AuthController`: login Google, refresh token e logout.
- `MeController`: dados do usuario autenticado.
- `PaymentsController`: planos, criacao de pagamento e webhook Mercado Pago.
- `ProviderKeysController`: cadastro de chaves Groq/Pollinations por usuario.
- `ChatbotController`: conversa com IA.
- `PeopleDiscoveryController`: busca de pessoas, posts e vagas.
- `OpportunityDiscoveryController`: busca de oportunidades.
- `ResumeImprovementsController`: melhoria e analise de curriculo.
- `InterviewAnalysisController`: analise de entrevistas.
- `HealthController`: health check.

### Frontend

O frontend fica em `FrontEnd/src/app`.

Rotas principais:

- `/`: landing page publica.
- `/comprar-creditos`: compra de creditos publica.
- `/login`: login Google e login de desenvolvimento local.
- `/dashboard`: dashboard autenticado e pago.
- `/credits`: compra de creditos dentro da aplicacao.
- `/chatbot`: chat administrativo.
- `/people-discovery`: busca de pessoas.
- `/people-discovery/posts`: busca de publicacoes.
- `/people-discovery/jobs`: busca de vagas por pessoas/prospeccao.
- `/opportunity-discovery`: busca de oportunidades.
- `/resume-improvements`: melhoria de curriculo.
- `/interview-analysis`: analise de entrevista.
- `/payment-required`: aviso de acesso pago necessario.

Guards principais:

- `authGuard`: exige usuario autenticado.
- `paymentGuard`: exige acesso pago ou creditos conforme regra da API.
- `adminGuard`: exige usuario admin.

## Banco de Dados

O projeto usa SQL Server com Entity Framework Core.

Tabelas principais:

- `Users`
- `RefreshTokens`
- `UserSubscriptions`
- `UserProviderApiKeys`
- `CreditPlans`
- `CreditPromotions`
- `PaymentOrders`
- `PeopleDiscoverySearchTexts`
- `PeopleDiscoverySavedSearches`
- `PeopleDiscoverySavedSearchResults`
- `OpportunitySearchTexts`
- `OpportunitySavedSearches`
- `OpportunitySavedSearchResults`
- `ResumeImprovementSessions`
- `ResumeImprovementMessages`
- `ResumeImprovementDocumentChunks`

Em desenvolvimento, a API executa `DatabaseInitializer.EnsureDevelopmentSchemaAsync`, que cria/ajusta tabelas e seeds essenciais, incluindo planos de credito, assinatura inicial do admin e campos adicionados durante a evolucao do produto.

## Configuracao

Arquivos principais:

- `Backend/src/Api/appsettings.json`
- `Backend/src/Api/appsettings.Development.json`
- `FrontEnd/src/environments/environment.ts`
- `FrontEnd/src/environments/environment.prod.ts`

Configuracoes esperadas no backend:

- `ConnectionStrings:DefaultConnection`
- `Jwt:Secret`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Google:ClientId`
- `Cors:FrontendOrigins`
- `MercadoPago:PublicKey`
- `MercadoPago:AccessToken`
- `MercadoPago:WebhookSecret`
- `MercadoPago:NotificationUrl`

Tambem e possivel sobrescrever valores por variaveis de ambiente:

```bash
JWT_SECRET="troque-por-uma-chave-com-32-caracteres-ou-mais"
JWT_ISSUER="Lessie"
JWT_AUDIENCE="Lessie.FrontEnd"
GOOGLE_CLIENT_ID="seu-client-id.apps.googleusercontent.com"
CONNECTION_STRING="Server=localhost,1433;Database=Lessie;User Id=sa;Password=sua-senha;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
FRONTEND_ORIGIN="http://localhost:4200"
MERCADO_PAGO_PUBLIC_KEY="sua-public-key"
MERCADO_PAGO_ACCESS_TOKEN="seu-access-token"
MERCADO_PAGO_WEBHOOK_SECRET="seu-webhook-secret"
MERCADO_PAGO_NOTIFICATION_URL="https://seu-dominio/api/payments/mercado-pago/webhook"
```

Observacao: os `appsettings` atuais podem conter chaves temporarias de desenvolvimento/publicacao. Antes de producao definitiva, gere novas credenciais e invalide as antigas.

## Rodar Localmente

### Backend

```bash
cd Backend
dotnet build src/Api/Lessie.Api.csproj
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Api/Lessie.Api.csproj --urls http://0.0.0.0:5000
```

Health check:

```bash
curl http://localhost:5000/health
```

### Frontend

Use Node 20.20.2 ou versao compativel com Angular 21.

```bash
cd FrontEnd
npm install
npm start
```

O script `npm start` ja executa:

```bash
ng serve --host 0.0.0.0 --allowed-hosts monstrinho
```

Acesse:

- Local: `http://localhost:4200`
- Rede local: `http://<ip-da-maquina>:4200`
- Host local configurado: `http://monstrinho:4200`

## Fluxo de Pagamento

1. O frontend lista planos ativos via API.
2. O usuario escolhe um plano de creditos.
3. A API cria um pedido local em `PaymentOrders`.
4. A API cria uma preferencia de pagamento no Mercado Pago.
5. O usuario e redirecionado para o Checkout Pro.
6. O Mercado Pago chama `MercadoPago:NotificationUrl`.
7. A API consulta/processa o pagamento.
8. Se aprovado, os creditos sao liberados na assinatura do usuario.

Durante desenvolvimento local sem URL publica, o webhook do Mercado Pago nao consegue chamar a maquina automaticamente. Para validar retorno real, a API precisa estar publicada ou exposta por tunel HTTPS.

## Comandos Uteis

```bash
# Backend
cd Backend
dotnet build src/Api/Lessie.Api.csproj
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Api/Lessie.Api.csproj --urls http://0.0.0.0:5000

# Frontend
cd FrontEnd
npm install
npm start
npm run build

# Git
git status
git add README.md .gitignore Backend FrontEnd Docs external tools
git commit -m "Initial Lessie version"
```

## Versionamento

Este repositorio deve versionar codigo-fonte, documentacao, configuracoes necessarias para desenvolvimento e integracoes em evolucao.

O `.gitignore` remove dependencias instaladas, caches, builds, logs, artefatos temporarios e arquivos locais de IDE. Ele nao ignora `appsettings*.json`, porque o projeto esta em fase de desenvolvimento e as credenciais atuais serao renovadas antes da producao definitiva.

Antes de publicar em producao:

- Renovar credenciais Google.
- Renovar tokens Mercado Pago.
- Renovar segredo JWT.
- Definir `MercadoPago:NotificationUrl` definitivo.
- Revisar CORS de producao.
- Trocar conexoes de banco temporarias por credenciais permanentes.
- Conferir se nao ha arquivos grandes ou gerados ja adicionados ao Git por acidente.
