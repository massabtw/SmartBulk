# 01: Solution, banco e DbUp

**What to build:** Ter a solução rodando localmente com banco versionado, para que todos os demais tickets tenham base comum. Ao subir a API, o banco SQL Server (Docker) é migrado automaticamente e um endpoint de saúde confirma a conexão.

**Blocked by:** None (can start immediately)

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Solution com os 4 projetos `SmartBulk.Domain/Application/Infrastructure/Api` e a regra de dependência Api → Application → Domain ← Infrastructure
- [x] Projetos de teste xUnit: unitário (Domain/Application) e integração (repositórios contra SQL Server em Docker)
- [x] `docker-compose` com SQL Server e instruções no README
- [x] DbUp aplica scripts SQL numerados da pasta `Database/` na inicialização; primeiro script cria a tabela de controle necessária
- [x] `Result<T>`, `UnitOfWork` com Dapper (conexão e transação) e `IoC` de cada camada seguindo a skill backend
- [x] Endpoint de saúde que executa uma consulta simples no banco e retorna sucesso
- [x] Teste de integração que sobe contra o banco e valida a migration e o endpoint de saúde
