# AGENTS.md

## Objetivo

Este arquivo orienta agentes de IA e colaboradores humanos a trabalhar neste repositório sem quebrar o fluxo atual do produto.

## Estrutura principal

- Backend: pasta Backend, com API, aplicação, domínio e infraestrutura.
- FrontEnd: aplicação Angular com rotas protegidas e fluxo de pagamentos.
- Docs: documentação de arquitetura, roadmap e épicos.
- external: integrações, MCPs e projetos auxiliares que podem evoluir de forma independente.

## Regras de contribuição

- Preserve a arquitetura e os padrões já empregados nas pastas existentes.
- Prefira mudanças pequenas e bem delimitadas em vez de reescrever módulos inteiros.
- Mantenha a documentação alinhada com mudanças de fluxo, rotas e integrações.
- Evite incluir segredos, tokens ou credenciais em arquivos rastreados.
- Use variáveis de ambiente ou arquivos locais não versionados para configuração sensível.

## Fluxo de trabalho assistido por IA

1. Leia a documentação relevante e os arquivos próximos antes de editar.
2. Respeite os contratos entre backend e frontend e preserve os endpoints e rotas existentes.
3. Quando possível, valide com o comando de build ou execução mais próximo do escopo da mudança.
4. Se a alteração afetar autenticação, pagamentos, integrações externas ou banco de dados, revise cuidadosamente os impactos antes de concluir.
