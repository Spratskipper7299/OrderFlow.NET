# AGENTS.md — Developer & AI Agent Guidelines for OrderFlow.NET

This document establishes mandatory rules and constraints for AI coding agents and developers working on the `OrderFlow.NET` repository.

---

## 1. Project Purpose & Scope

* **Core Function:** OrderFlow.NET is a distributed order-processing system built on **.NET 9 / C# 13**.
* **Primary Flow:** `Create Order → Reserve Inventory → Authorize Payment → Confirm Order` (or `Release Inventory → Reject Order` on payment authorization failure).
* **System Goals:** Demonstrates realistic distributed systems patterns: asynchronous messaging, transactional outbox, inbox/idempotency, saga state machines, optimistic concurrency, eventual consistency, and database-per-service.
* **Scope Boundary:** This is an intentionally small core system. No shipping, multi-tenancy, event sourcing, separate read databases, or external payment gateways exist in v1/v2.

---

## 2. Architectural Source of Truth

* **Primary Documents:**
  * [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — System architecture, domain state machines, infrastructure contracts, and failure workflows.
  * [`docs/DECISIONS.md`](docs/DECISIONS.md) — Architectural Decision Records (ADRs) defining **why** specific design choices were made.
* **Agent Rule:** AI agents **MUST NOT** redesign the architecture, introduce new frameworks, add extra microservices, or bypass established ADRs without explicit user instructions.

---

## 3. Service Boundaries & Project Ownership

* **Target Architecture:** Three independent business services running as separate processes:
  * `OrderFlow.Order` (Domain, Application, Infrastructure, Api)
  * `OrderFlow.Inventory` (Domain, Application, Infrastructure, Worker)
  * `OrderFlow.Payment` (Domain, Application, Infrastructure, Worker)
  * `OrderFlow.Contracts` (Shared message contracts only)
* **Current Codebase State:** The architecture is a three-service distributed order-processing system:
  * Order service (`OrderFlow.Order.*`)
  * Inventory service (`OrderFlow.Inventory.*`)
  * Payment service (`OrderFlow.Payment.*`)
  * Shared contracts (`OrderFlow.Contracts`)
* **Contract Rule:** Services share **ONLY** contracts in `OrderFlow.Contracts`. A service must **NEVER** reference another service's Domain, Application, or Infrastructure projects.

---

## 4. Database Ownership & EF Core Migrations

* **Database-per-Service:** Each service owns its own database (`OrderDb`, `InventoryDb`, `PaymentDb`).
* **No Direct Access:** No service may directly query or write to another service's database.
* **Migration Ownership:** Each service owns and executes its own EF Core migrations. Never create cross-service DB migrations or shared DbContext schemas across boundaries.

---

## 5. Messaging & MassTransit / RabbitMQ Rules

* **Transport:** RabbitMQ is the asynchronous messaging transport; MassTransit manages topology, endpoints, retries, and outbox/inbox handlers.
* **Explicit Message Contracts:** Messages must be strongly typed and declared in `OrderFlow.Contracts`.
  * **Commands:** Explicit actions (e.g., `ReserveInventory`, `AuthorizePayment`, `ReleaseInventory`).
  * **Events:** Explicit facts (e.g., `OrderCreated`, `InventoryReserved`, `InventoryRejected`, `PaymentAuthorized`, `PaymentRejected`, `OrderConfirmed`, `OrderRejected`).
* **Anti-Pattern:** Generic or opaque messages like `UpdateEntity` or `ProcessData` are strictly forbidden.

---

## 6. Transactional Outbox (Dual-Write Prevention)

* **Mandatory Usage:** Any business transaction that publishes integration messages **MUST** use MassTransit's EF Core Transactional Outbox.
* **Atomic Commit:** Business entity changes and outbox records **MUST** commit in the same SQL transaction.
* **Broker Boundary:** RabbitMQ is **NOT** part of the SQL transaction. Message delivery occurs asynchronously after the local SQL transaction successfully commits.

---

## 7. Inbox & Consumer Idempotency

* **At-Least-Once Delivery:** Duplicate message redelivery MUST be expected and handled gracefully.
* **MassTransit Inbox:** All consumers MUST use MassTransit Inbox / Idempotency mechanisms to ensure duplicate delivery of messages (e.g., `OrderCreated`, `AuthorizePayment`) does not cause duplicate side effects (e.g., double stock reservation, double payment charge).

---

## 8. Redis HTTP Idempotency

* **Purpose:** Redis is used **ONLY** for HTTP-level API idempotency (`Idempotency-Key` header on `POST /api/orders`).
* **Non-Goals:** Redis must **NOT** be used for inventory locks, distributed mutexes, message idempotency, order state, or payment records.
* **Fail-Closed Policy:** If Redis is unreachable during `POST /api/orders`, the API MUST fail closed and return `503 Service Unavailable`.

---

## 9. Saga Orchestration & Workflow State Machine

* **Orchestration Location:** The distributed order workflow is coordinated by a MassTransit State Machine Saga in `OrderFlow.Order`.
* **Responsibility Separation:**
  * **Saga:** Manages step sequence, state transitions, and triggering compensation actions.
  * **Domain Services:** Enforce their own business rules (Order rules in Order service, Inventory rules in Inventory service, Payment rules in Payment service).
* **Anti-Pattern:** Do **NOT** place domain validation or business logic inside the saga state machine.

---

## 10. Compensation & Eventual Consistency

* **No Distributed Transactions:** The system relies on eventual consistency instead of distributed ACID transactions across services.
* **Compensation Path:** When payment authorization fails after inventory reservation, the saga MUST issue a compensating command (`ReleaseInventory`) to undo the stock reservation. SQL rollbacks across services are impossible.

---

## 11. Inventory Concurrency & Stock Rules

* **Optimistic Concurrency:** Inventory stock updates MUST use SQL Server optimistic concurrency via EF Core `RowVersion` (`byte[]`). Distributed Redis locks are forbidden.
* **Stock Fields:** `Product` tracks `AvailableQuantity` and `ReservedQuantity`.
* **Stock Lifecycle Rules:**
  * **Reserve:** `AvailableQuantity -= qty`, `ReservedQuantity += qty` (Requires `AvailableQuantity >= qty`).
  * **Release (Compensation):** `AvailableQuantity += qty`, `ReservedQuantity -= qty`.
  * **Commit (Payment Success):** `ReservedQuantity -= qty`.
* **Invariants:** `AvailableQuantity` and `ReservedQuantity` must **NEVER** become negative.

---

## 12. Payment Service Boundaries

* **Simulation in v1/v2:** Payment authorization is simulated internally by `OrderFlow.Payment`. No external HTTP payment gateway integration (Stripe, Adyen, etc.) is allowed in v1/v2.
* **Payment Idempotency:** Duplicate `AuthorizePayment` commands for the same `OrderId` must be idempotent and return the existing payment result without creating duplicate charges.

---

## 13. CQRS & MediatR Usage

* **In-Process Only:** MediatR is used **ONLY** inside a service for in-process command/query dispatching and pipeline behaviors (logging, validation).
* **Cross-Process Boundary:** MediatR must **NEVER** be used for cross-service communication (RabbitMQ/MassTransit must be used instead).
* **Database Architecture:** CQRS in this project uses the service's primary database for both reads and writes. A separate read database or event sourcing read model is prohibited in v1/v2.

---

## 14. Clean Architecture & Domain Separation

* **Infrastructure-Free Domain:** Domain projects (`*.Domain`) MUST be pure C# and MUST NOT reference EF Core, ASP.NET Core, MassTransit, RabbitMQ, Redis, or MediatR.
* **Intent-Revealing Repositories:** Prefer explicit business methods (`ReserveStock`, `ReleaseStock`, `CommitReservation`) over generic CRUD (`Update`, `Save`).

---

## 15. Error Handling & Retry Policies

* **Technical / Transient Failures:** SQL timeouts, broker blips, or network glitches are retried using MassTransit exponential backoff policies. Exhausted retries move messages to MassTransit error queues.
* **Business Failures:** Insufficient stock, declined payment, or invalid domain state are **business outcomes**, NOT technical failures. They MUST produce explicit business events (`InventoryRejected`, `PaymentRejected`) and must **NOT** be retried indefinitely.

---

## 16. Testing Expectations & Verification

* **Unit Tests:** Fast, isolated unit tests for domain invariants (stock checks, line validation, state transitions).
* **Integration Tests:** Integration tests use Testcontainers for SQL Server, RabbitMQ, and Redis.
* **Mandatory Failure Scenarios to Test:**
  * Duplicate HTTP `Idempotency-Key`
  * Duplicate message redelivery
  * Insufficient stock rejection
  * Payment rejection & inventory release compensation
  * Concurrency conflicts on stock reservation (ensuring stock never goes negative)
  * Outbox atomicity and error queue routing

---

## 17. Docker & Local Development

* **Local Infrastructure:** `docker-compose.yml` runs SQL Server, RabbitMQ, and Redis alongside the application services (`api`, `worker`).
* **Environment Integrity:** Ensure health checks pass before running integration tests or local verification.

---

## 18. Anti-Complexity & Incremental Workflow

* **No Unrequested Patterns:** Do NOT add event sourcing, GraphQL, Kubernetes manifests, gateway services, or notification workers unless explicitly instructed.
* **Incremental Slices:** Implement feature slices end-to-end (Domain -> Application -> Infrastructure -> Messaging -> Test verification) without introducing speculative complexity.

---

## 19. Keeping Documentation Synchronized

* **Documentation Precedence:** When implementing changes, ensure full alignment with [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and [`docs/DECISIONS.md`](docs/DECISIONS.md).
* **Reporting Discrepancies:** If code or requirements conflict with documented ADRs, report the conflict before modifying architectural rules.
