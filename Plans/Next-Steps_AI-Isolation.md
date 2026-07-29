# Next Steps — AI-Isolation e Backmerge

Visão geral
Após a migração dos artefatos de IA para AI-Isolation, a estabilização do fluxo de backmerge e a validação básica de YAML, este documento define os próximos passos para consolidar a separação, validar a qualidade de código/configuração e preparar a revisão por pull request.

Tarefas pendentes (ordem sugerida)
- [x] Validar YAML do workflow backmerge-develop.yml com validação estável (PyYAML já testado; yamllint recomendado)
- [x] Verificar novamente o backmerge-develop.yml com PyYAML (OK)
- [x] Migração de artefatos IA para AI-Isolation concluída (renomeação e reorganização)
- [x] Atualizar Migration-Notes_AI-Isolation.md com alterações recentes e decisões
- [x] Atualizar Migration-Plan_AI-Isolation.md conforme evolução (em progresso)
- [ ] Rodar lint estático no código relacionado (ex.: FrontEnd/BackEnd conforme necessidade)
- [ ] Executar yamllint (CI/local)
- [ ] Abrir PR consolidando mudanças (workflow, documentação e reorganização de IA)
- [ ] Verificar referências de IA nos docs de FrontEnd/BackEnd e atualizá-las

Notas de progresso e critérios de sucesso
- Progresso atual: migração concluída, workflow está estável (validado com PyYAML), documentação atualizada conforme as mudanças. Em andamento a validação adicional com yamllint no CI/local.
- Critérios de sucesso:
  - Workflow backmerge-develop.yml livre de erros de YAML e de nested mappings
  - Artefatos IA isolados de forma estável, com documentação correspondente
  - PR consolidado com as mudanças no repositório remoto
  - Planos de migração e retirada completos e atualizados

Planos de integração contínua (operacionais)
- Incluir yamllint no pipeline CI para validação contínua de YAML
- Manter documentação sincronizada com mudanças no isolamento de IA

Observações finais
- Este documento é vivo e será iterado conforme feedback. As etapas já marcadas como concluídas representam o estado atual, e as pendentes devem ser executadas para fechar o backlog de planejamento.