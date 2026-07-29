Plan de Ação - Isolamento AI e Backmerge

Resumo
Este plano descreve as ações para isolar artefatos de IA da raiz do repositório, consolidar o conteúdo de IA em uma pasta AI-Isolation, ajustar fluxos de CI/CD para não depender de integrações de IA, e manter um fluxo de backmerge estável entre main e develop.

Estado atual
- Branch: feat/ai-isolation-branch-setup-3
- Arquivos relevantes: AI-Isolation/README.md, Migration-Notes_AI-Isolation.md, Backmerge-develop.yml atual (em progresso)
- Estrutura: AI-Isolation/ contendo subpastas .ai, .cline; Documentation/Understanding/General-Overview.md etc.

Objetivos
- Isolar artefatos de IA da raiz, com remoção segura.
- Garantir que o fluxo de backmerge não dependa de condicionais YAML propensos a erros.
- Fornecer documentação de migração para facilitar retirada futura.

Escopo
- Movimentação de conteúdo IA
- Criação de documentação de migração (Migration-Notes_AI-Isolation.md, Migration-Plan.md)
- Atualização de .github/workflows/backmerge-develop.yml para uma versão estável
- Criação de um plano de retirada segura (checklist)

Requisitos de sucesso
- Workflow estável (sem nested mappings)
- Documentação atualizada
- Commitados no branch de features
- PR pronto para revisão

Plano de Ação (checklist)
- [x] Confirmar estado atual do branch com o repositório
- [x] Criar Migration-Notes_AI-Isolation.md (já criado)
- [x] Criar Migration-Plan.md (planejado)
- [x] Consolidar estrutura AI-Isolation (pastas .ai, .cline)
- [x] Atualizar backmerge-develop.yml para forma estável
- [ ] Rodar lint de YAML (yamllint) e ajustar qualquer falha
- [ ] Criar Migration-Notes adicional com etapas de retirada
- [ ] Criar PR de revisão com as mudanças

Critérios de sucesso adicionais
- Sem regressões em builds
- Facilidade de manutenção futura
- Documentação completa

Riscos
- Risco de elementos IA não movidos. Mitigação: varredura adicional
- Risco de dependências externas. Mitigação: isolamento e documentação

Aprovação
- Este plano está sujeito a ajustes conforme feedback.