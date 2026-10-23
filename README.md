# SmartBulk

Sistema de automação nutricional, rastreamento de dieta e planejador de compras com lógica de **cálculo reverso via refeitório corporativo**.

---

## 🏛️ Arquitetura do Backend (.NET)

O backend segue a arquitetura em 4 camadas recomendada pelo Grupo Madero:
- **`SmartBulk.Domain`**: Entidades ricas, enums, interfaces de repositórios/UnitOfWork, regras puras de cálculo nutricional (sem dependências externas).
- **`SmartBulk.Application`**: Casos de uso/Services, Models (POCO), Mappers manuais estáticos (`XModelMap`), validações e IoC.
- **`SmartBulk.Infrastructure`**: Acesso a dados via **Dapper**, implementação do `UnitOfWorkApp`, migrações com **DbUp** e clientes HTTP.
- **`SmartBulk.Api`**: Controllers RESTful magros, injeção via `[FromServices]`, middleware e endpoints de saúde.

Regra de dependência: `Api → Application → Domain ← Infrastructure`.

---

## 🗄️ Banco de Dados e Migrações (DbUp)

O projeto utiliza **SQL Server** com migrações automáticas versionadas via **DbUp**.
Os scripts SQL são numerados e armazenados na pasta [`Database/`](Database/). Na inicialização da aplicação, o DbUp verifica e executa automaticamente quaisquer novos scripts pendentes.

### Opção 1: Rodando SQL Server via Docker

Para subir a instância do SQL Server no Docker:

```bash
docker compose up -d
```

String de conexão correspondente (`appsettings.json`):
```json
"ConnectionStrings": {
  "AppDb": "Server=localhost,1433;Database=SmartBulk;User Id=sa;Password=Your_Strong_Password123!;TrustServerCertificate=True;"
}
```

Para parar o contêiner:
```bash
docker compose down
```

### Opção 2: Rodando via SQL Server LocalDB (Windows)

Caso esteja no ambiente Windows com LocalDB (`MSSQLLocalDB`):

```bash
sqllocaldb start MSSQLLocalDB
```

String de conexão correspondente (`appsettings.Development.json`):
```json
"ConnectionStrings": {
  "AppDb": "Server=(localdb)\\MSSQLLocalDB;Database=SmartBulk;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

---

## 🚀 Como Executar a API

1. Inicie o banco de dados (Docker ou LocalDB).
2. Execute o projeto API:

```bash
dotnet run --project src/SmartBulk.Api
```

3. Verifique a saúde da aplicação e do banco de dados acessando:
   - `GET http://localhost:5000/health` ou `GET http://localhost:5000/api/health`

Exemplo de resposta:
```json
{
  "status": "Healthy",
  "timestamp": "2026-10-23T18:00:00Z",
  "database": "Connected",
  "systemStatus": "Initialized"
}
```

---

## 🧪 Como Executar os Testes

Para executar toda a suíte de testes (unitários e de integração):

```bash
dotnet test
```

Para executar apenas os testes unitários:
```bash
dotnet test tests/SmartBulk.UnitTests
```

Para executar apenas os testes de integração:
```bash
dotnet test tests/SmartBulk.IntegrationTests
```

