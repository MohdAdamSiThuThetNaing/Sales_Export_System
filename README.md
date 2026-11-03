
# Sales Data Export System

## 1. Project Overview

This project implements an asynchronous sales data export system.

Users submit export requests through a Python FastAPI REST API. Requests are stored in PostgreSQL. A .NET 8 Worker Service claims pending requests, retrieves matching sales transactions, generates CSV files, and updates the request status.

### Architecture

- **API:** Python, FastAPI, SQLAlchemy
- **Worker:** C# .NET 8 Worker Service
- **Database:** PostgreSQL 16
- **Output:** CSV files

### Request Workflow

1. Client submits an export request.
2. FastAPI validates the request and stores it with `Pending` status.
3. The .NET worker claims a pending request.
4. The worker retrieves matching sales records through `IDataConnector`.
5. The worker streams records into a CSV file.
6. The worker updates the request to `Completed` or `Failed`.
7. Client checks the request status through the API.

## 2. Running the Project

### Start PostgreSQL

```bash
docker compose up -d
```

### Start FastAPI

```bash
cd api
source .venv/bin/activate
uvicorn app.main:app --reload
```

API documentation: http://127.0.0.1:8000/docs

### Start .NET Worker

Open another terminal:

```bash
cd worker/SalesExport.Worker
dotnet run
```

The worker runs as a console-hosted Worker Service during local macOS development. It can be configured to run as a Windows Service on Windows.

## 3. Database Performance

Original query:

```sql
SELECT *
FROM SalesTransactions
WHERE YEAR(TransactionDate) = 2025
  AND CustomerName LIKE '%corp%'
ORDER BY Amount DESC;
```

Optimized PostgreSQL query:

```sql
SELECT id, region, transaction_date, customer_name, amount
FROM sales_transactions
WHERE transaction_date >= DATE '2025-01-01'
  AND transaction_date < DATE '2026-01-01'
  AND customer_name ILIKE '%corp%'
ORDER BY amount DESC;
```

### Optimization Explanation

- Replaced the year function with a date range so a B-tree index on the date column can be used.
- Selected only the required columns instead of using `SELECT *`.
- Used `ILIKE` for case-insensitive matching in PostgreSQL.
- A leading wildcard in `%corp%` prevents a normal B-tree index from efficiently supporting the substring search.
- PostgreSQL's `pg_trgm` extension and a GIN trigram index can improve substring searches:

```sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX idx_sales_customer_name_trgm
ON sales_transactions
USING GIN (customer_name gin_trgm_ops);
```

- Used `EXPLAIN (ANALYZE, BUFFERS)` to inspect the execution plan and measure query performance.

## 4. Concurrency and Duplicate Processing Prevention

The worker uses PostgreSQL transactions and `SELECT ... FOR UPDATE SKIP LOCKED` when claiming jobs.

This ensures that concurrent worker instances skip rows already locked by another transaction and claim different pending jobs.

Additional reliability measures:

- Each claimed request is assigned a worker ID.
- A lease expiration time is stored for each processing request.
- Workers periodically renew their leases.
- Expired jobs can be recovered and returned to `Pending`.
- Status updates verify worker ownership.
- CSV files are written to temporary files before being published.

## 5. Design Decisions

### Why FastAPI instead of Django?

FastAPI is suitable for this project because it is an API-first service. It provides automatic OpenAPI documentation, Pydantic validation, type hints, and asynchronous request handling.

Django would be a good choice for a larger full-stack portal requiring built-in authentication, admin functionality, and a comprehensive ORM.

### Reusing a .NET Framework 4.6 Library in .NET 8

A .NET Framework 4.6 library cannot generally be referenced directly as a supported dependency from .NET 8.

Possible approaches:

- Retarget or multi-target the library to a compatible framework such as .NET Standard 2.0, if its APIs allow it.
- Extract shared business logic into a compatible library.
- Replace unsupported framework-specific APIs.
- Run the legacy library in a separate .NET Framework process and communicate through HTTP, gRPC, or messaging.

.NET Standard 2.0 compatibility generally requires the legacy .NET Framework application to target at least .NET Framework 4.6.1.

## 6. Future Improvements

- Add authentication and authorization.
- Add retry policies with exponential backoff.
- Add automated unit, integration, and concurrency tests.
- Add structured logging, metrics, and monitoring.
- Store generated files in object storage.
- Add request idempotency and rate limiting.
- Add CSV download endpoints with appropriate access control.
- Add deployment automation and Windows Service installation testing.
