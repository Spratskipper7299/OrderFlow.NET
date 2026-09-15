# OrderFlow.NET Architecture

Living design for a small distributed order-processing system on **.NET 9 / C# 13**.

This document describes architectural intent. Implementation follows in incremental slices. Nothing here is a production-readiness claim.

## Goal

OrderFlow.NET demonstrates a distributed order-processing workflow with independent Order, Inventory, and Payment services.

The system is intentionally small, but it demonstrates realistic distributed-system concerns:

* asynchronous messaging
* transactional outbox
* inbox/idempotent consumers
* eventual consistency
* optimistic concurrency
* retries
* failure handling
* compensating actions
* saga orchestration
* database-per-service
* HTTP idempotency

The main business flow is:

**Create order → reserve inventory → authorize payment → confirm order**

If payment fails after inventory has been reserved, the workflow compensates by releasing the reservation before rejecting the order.

No shipping, multi-tenancy, event sourcing, or real external payment provider is included in v1/v2.

---

## High-Level Architecture

OrderFlow.NET consists of three independent business services:

* `OrderFlow.Order` — HTTP API and order workflow orchestration
* `OrderFlow.Inventory` — inventory reservation and stock management
* `OrderFlow.Payment` — payment authorization simulation

RabbitMQ is the asynchronous communication boundary.

Each service owns its own database and persistence model.

```text
                         ┌──────────────┐
                         │    Client    │
                         └──────┬───────┘
                                │
                                ▼
                         ┌─────────────┐
                         │ Order API   │
                         │   Service   │
                         └──────┬──────┘
                                │
                         HTTP Idempotency
                                │
                              Redis
                                │
                                ▼
                         ┌─────────────┐
                         │ Order DB    │
                         └──────┬──────┘
                                │
                              Outbox
                                │
                                ▼
                           RabbitMQ
                         ╱           ╲
                        ▼             ▼
               ┌──────────────┐ ┌──────────────┐
               │   Inventory  │ │   Payment    │
               │   Service    │ │   Service    │
               └──────┬───────┘ └──────┬───────┘
                      │                  │
                      ▼                  ▼
               ┌──────────────┐ ┌──────────────┐
               │ Inventory DB │ │  Payment DB  │
               └──────┬───────┘ └──────┬───────┘
                      │                  │
                    Outbox             Outbox
                      │                  │
                      └────────┬─────────┘
                               ▼
                           RabbitMQ
                               │
                               ▼
                         Order Service
                               │
                         Saga / Workflow
                               │
                     ┌─────────┴─────────┐
                     ▼                   ▼
                 Confirmed            Rejected
```

This is a distributed system with separate process and database ownership boundaries. It is intentionally not a large microservice mesh.

---

# Services

## 1. Order Service

Project:

`OrderFlow.Order`

Responsibilities:

* expose REST API
* create orders
* query order state
* own the Order aggregate
* own order workflow state
* coordinate the distributed order workflow
* host the saga state machine
* persist order-related outbox messages
* handle HTTP idempotency through Redis

The Order service does not directly modify Inventory or Payment databases.

It communicates with those services through RabbitMQ messages.

---

## 2. Inventory Service

Project:

`OrderFlow.Inventory`

Responsibilities:

* own product/inventory data
* reserve inventory
* release reservations
* commit reservations after successful payment
* reject reservations when stock is insufficient
* protect inventory consistency through optimistic concurrency
* consume inventory-related commands
* publish inventory events through its transactional outbox

The Inventory service owns the inventory database.

The Order service must not directly access it.

---

## 3. Payment Service

Project:

`OrderFlow.Payment`

Responsibilities:

* own payment authorization state
* process payment authorization requests
* simulate successful and failed payment authorization
* publish payment results
* consume payment-related commands
* persist payment-related outbox messages

No real external payment provider is required in the initial implementation.

The Payment service owns the payment database.

The Order or Inventory services must not directly access it.

---

# Service Boundaries

Each service owns:

* its domain model
* application logic
* infrastructure implementation
* database
* database migrations
* outbox/inbox persistence
* service-specific repositories

Services share only message contracts.

They do not reference each other's Domain, Application, or Infrastructure projects.

```text
Order Service
    │
    ├── Domain
    ├── Application
    ├── Infrastructure
    └── API

Inventory Service
    │
    ├── Domain
    ├── Application
    ├── Infrastructure
    └── Worker

Payment Service
    │
    ├── Domain
    ├── Application
    ├── Infrastructure
    └── Worker

OrderFlow.Contracts
    ├── Orders
    ├── Inventory
    └── Payments
```

---

# Solution Layout

```text
OrderFlow.NET.sln
│
├── src/
│
│   ├── OrderFlow.Contracts/
│   │   ├── Orders/
│   │   ├── Inventory/
│   │   └── Payments/
│   │
│   ├── OrderFlow.Order/
│   │   ├── Domain/
│   │   │   ├── Orders/
│   │   │   └── ...
│   │   │
│   │   ├── Application/
│   │   │   ├── Orders/
│   │   │   │   ├── Commands/
│   │   │   │   └── Queries/
│   │   │   ├── Behaviors/
│   │   │   └── ...
│   │   │
│   │   ├── Infrastructure/
│   │   │   ├── Data/
│   │   │   ├── Messaging/
│   │   │   ├── Redis/
│   │   │   └── Repositories/
│   │   │
│   │   └── Api/
│   │
│   ├── OrderFlow.Inventory/
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Infrastructure/
│   │   └── Worker/
│   │
│   └── OrderFlow.Payment/
│       ├── Domain/
│       ├── Application/
│       ├── Infrastructure/
│       └── Worker/
│
├── tests/
│   ├── OrderFlow.Order.UnitTests/
│   ├── OrderFlow.Inventory.UnitTests/
│   ├── OrderFlow.Payment.UnitTests/
│   └── OrderFlow.IntegrationTests/
│
├── docs/
│   ├── docker-compose.yml
│   └── ...
│
├── ARCHITECTURE.md
└── DECISION.md
```

The exact namespace/project naming may evolve during implementation, but service ownership boundaries must remain explicit.

---

# Distributed Order Workflow

The primary workflow is:

```text
Create Order
     │
     ▼
Order: Pending
     │
     │ OrderCreated
     ▼
Inventory Service
     │
     ├──────── insufficient stock ────────► InventoryRejected
     │
     ▼
InventoryReserved
     │
     ▼
Payment Service
     │
     ├──────── authorization failed ─────► PaymentRejected
     │
     ▼
PaymentAuthorized
     │
     ▼
Order: Confirmed
```

Compensation path:

```text
PaymentRejected
       │
       ▼
ReleaseInventory
       │
       ▼
InventoryReleased
       │
       ▼
Order: Rejected
```

The workflow is eventually consistent.

There is no distributed ACID transaction spanning all services.

---

# Order State

The Order aggregate has the following lifecycle:

```text
Pending
   │
   ├── InventoryRejected ─────────────► Rejected
   │
   ▼
InventoryReserved
   │
   ├── PaymentRejected ───────────────► Rejected
   │
   ▼
PaymentAuthorized
   │
   ▼
Confirmed
```

Only valid transitions are allowed.

The Order aggregate remains the consistency boundary for order state.

`RejectionReason` is stored when an order is rejected.

---

# Saga / Workflow Orchestration

The distributed workflow is coordinated through a saga state machine.

The saga does not own business rules belonging to individual services.

Instead:

* Order service owns order rules.
* Inventory service owns inventory rules.
* Payment service owns payment rules.
* Saga coordinates the sequence and compensation.

Conceptually:

```text
OrderCreated
     │
     ▼
ReserveInventory
     │
     ├── InventoryRejected
     │        │
     │        ▼
     │      RejectOrder
     │
     ▼
InventoryReserved
     │
     ▼
AuthorizePayment
     │
     ├── PaymentRejected
     │        │
     │        ▼
     │   ReleaseInventory
     │        │
     │        ▼
     │   InventoryReleased
     │        │
     │        ▼
     │     RejectOrder
     │
     ▼
PaymentAuthorized
     │
     ▼
ConfirmOrder
```

MassTransit State Machine is the intended orchestration mechanism.

The saga state is persisted independently from the business aggregates.

---

# CQRS

CQRS is used inside each service where it provides a meaningful separation between commands and queries.

MediatR is used for in-process command/query dispatching and pipeline behaviors.

Command flow:

```text
HTTP Command
    ↓
MediatR
    ↓
Pipeline Behaviors
    ↓
Command Handler
    ↓
Command Repository
    ↓
DbContext
```

Query flow:

```text
HTTP Query
    ↓
MediatR
    ↓
Pipeline Behaviors
    ↓
Query Handler
    ↓
Query Repository
    ↓
DbContext
```

Command handlers depend only on command-side abstractions.

Query handlers depend only on query-side abstractions.

No separate read database or event-sourced read model is required.

---

# Messaging

RabbitMQ is the asynchronous transport.

MassTransit manages:

* message consumers
* endpoint configuration
* retries
* EF Core outbox
* inbox/idempotency
* message topology
* error queues
* saga state machine integration

Messages are represented by contracts in:

`OrderFlow.Contracts`

Example contracts:

```text
Orders/
    OrderCreated.cs
    OrderConfirmed.cs
    OrderRejected.cs

Inventory/
    ReserveInventory.cs
    InventoryReserved.cs
    InventoryRejected.cs
    ReleaseInventory.cs
    InventoryReleased.cs

Payments/
    AuthorizePayment.cs
    PaymentAuthorized.cs
    PaymentRejected.cs
```

Commands and events must be explicit about their intent.

---

# Transactional Outbox

Every service that changes business data and publishes a message uses a transactional outbox.

Example:

```text
Inventory Service

BEGIN TRANSACTION

UPDATE inventory

INSERT outbox message:
    InventoryReserved

COMMIT
```

Only after the database transaction commits can the outbox dispatcher deliver the message to RabbitMQ.

If the database transaction rolls back:

```text
Business change → ROLLBACK
Outbox message  → ROLLBACK
RabbitMQ publish → does not occur
```

The system does not attempt to make RabbitMQ itself part of the SQL transaction.

The outbox solves the dual-write problem between the local database and message broker.

---

# Inbox and Idempotent Consumers

Consumers use MassTransit's inbox functionality to avoid processing the same logical message more than once.

Example:

```text
OrderCreated
     │
     ▼
Inventory Consumer
     │
     ├── first delivery → reserve stock
     │
     └── duplicate     → ignored
```

This is particularly important for inventory operations.

A redelivered message must not decrement stock twice.

Consumer operations must also be designed to be safely repeatable where practical.

---

# RabbitMQ Failure Semantics

RabbitMQ does not provide SQL-style rollback.

If a consumer fails:

```text
RabbitMQ
    ↓
Consumer
    ↓
Exception
    ↓
Retry
    ↓
Retry
    ↓
Retry
    ↓
Error Queue
```

Transient failures are retried with exponential backoff.

Business failures are not treated as transient infrastructure failures.

Examples:

```text
Insufficient stock
Payment declined
Invalid business state
```

should result in explicit business events rather than infinite retries.

Permanent/unhandled technical failures eventually reach the MassTransit error queue.

---

# Database-per-Service

Each service owns its own database.

```text
OrderFlow.Order
    └── OrderDb

OrderFlow.Inventory
    └── InventoryDb

OrderFlow.Payment
    └── PaymentDb
```

No service directly queries another service's database.

Cross-service data access happens through messages or explicit APIs where required.

Each service manages its own EF Core migrations.

There is no distributed database transaction.

---

# Order Database

The Order service owns:

```text
Order
OrderLine
SagaState
Outbox / Inbox tables
```

The exact persistence representation of saga state is infrastructure-owned.

---

# Inventory Database

The Inventory service owns:

```text
Product
Inventory reservation state
Outbox / Inbox tables
```

Inventory uses optimistic concurrency.

A product contains at least:

```text
Id
Sku
Name
AvailableQuantity
ReservedQuantity
RowVersion
```

Reservation rules:

```text
AvailableQuantity >= requested quantity
```

On reservation:

```text
AvailableQuantity -= quantity
ReservedQuantity += quantity
```

On release:

```text
AvailableQuantity += quantity
ReservedQuantity -= quantity
```

On successful payment:

```text
ReservedQuantity -= quantity
```

`AvailableQuantity` must never become negative.

---

# Payment Database

The Payment service owns:

```text
Payment
PaymentAttempt
Outbox / Inbox tables
```

The initial implementation simulates authorization.

A payment should be associated with an OrderId.

Payment authorization must be idempotent.

A duplicate authorization request must not create multiple successful payment records for the same logical operation.

---

# Optimistic Concurrency

Inventory correctness is protected through SQL Server optimistic concurrency.

`Product.RowVersion` is used by EF Core.

Example:

```text
Stock = 5

Order A → reserve 4
Order B → reserve 4
```

One transaction succeeds.

The other encounters a concurrency conflict and is retried.

After re-reading the current state, the second operation sees:

```text
AvailableQuantity = 1
```

and produces:

```text
InventoryRejected
```

rather than creating negative stock.

Distributed locks are intentionally not used for inventory correctness.

---

# Redis

Redis is used only for HTTP-level idempotency.

Example:

```text
POST /api/orders
Idempotency-Key: abc-123
```

Redis stores:

```text
abc-123 → OrderId
```

with a TTL.

Redis is not used for:

* inventory locks
* distributed mutexes
* order source of truth
* payment state
* RabbitMQ message idempotency

RabbitMQ consumer idempotency is handled through MassTransit inbox functionality.

If Redis is unavailable, the create-order endpoint fails closed with `503 Service Unavailable`.

---

# HTTP Idempotency

The create-order endpoint supports:

```http
Idempotency-Key: <client-generated-key>
```

The same key must not create multiple orders.

The idempotency mechanism protects against:

* client retries
* network timeouts
* duplicate HTTP requests

This is separate from message-level idempotency.

```text
HTTP Idempotency
    ↓
Redis

Message Idempotency
    ↓
MassTransit Inbox
```

---

# Domain Model

## Order

The Order aggregate owns its OrderLines.

At minimum:

```text
Order
    Id
    CustomerId
    Status
    CreatedAt
    RejectionReason
    RowVersion
    Lines
```

Invariants:

* at least one line
* quantity > 0
* unit price >= 0
* duplicate product lines are not allowed
* `Pending → InventoryReserved | Rejected`
* `InventoryReserved → PaymentAuthorized | Rejected`
* `PaymentAuthorized → Confirmed`

The domain does not depend on:

* EF Core
* MassTransit
* ASP.NET Core
* RabbitMQ
* Redis

---

## Inventory

Inventory owns stock invariants.

Rules include:

* quantity cannot be negative
* available quantity cannot become negative
* reserved quantity cannot become negative
* reservation requires sufficient available stock
* release requires an existing reservation
* a reservation cannot be committed twice

---

## Payment

Payment owns payment invariants.

Rules include:

* payment belongs to an order
* an authorization operation is idempotent
* an already authorized payment cannot be authorized again as a new payment
* rejected payments remain rejected unless an explicit retry policy allows another attempt

---

# REST API

The Order service exposes:

```text
POST /api/orders
GET  /api/orders/{id}
```

Create order:

```text
201 Created
400 Bad Request
409 Conflict
503 Service Unavailable
```

Query order:

```text
200 OK
404 Not Found
```

Swagger is enabled in Development.

The API does not directly publish workflow messages during the HTTP request.

The request persists business data and the outbox record in the same transaction.

---

# Failure Scenarios

## Inventory failure

```text
OrderCreated
    ↓
Inventory service unavailable
    ↓
Retry
    ↓
Eventually succeeds
```

If retries are exhausted:

```text
Error Queue
```

The order remains in its current workflow state until recovery/reprocessing policy handles the failed message.

---

## Insufficient inventory

```text
ReserveInventory
    ↓
Insufficient stock
    ↓
InventoryRejected
    ↓
OrderRejected
```

No retry is required because insufficient stock is a business outcome.

---

## Payment failure

```text
InventoryReserved
    ↓
PaymentRejected
    ↓
ReleaseInventory
    ↓
InventoryReleased
    ↓
OrderRejected
```

The inventory reservation is compensated rather than silently abandoned.

---

## Duplicate message

```text
OrderCreated
OrderCreated
```

The second delivery is detected by inbox/idempotency mechanisms and must not reserve stock twice.

---

## Concurrent inventory requests

Multiple orders may attempt to reserve the same product simultaneously.

SQL optimistic concurrency ensures that conflicting updates are detected.

The final state must never contain negative available inventory.

---

# Testing Strategy

## Unit Tests

Each service has domain and application tests.

### Order

* valid creation
* invalid lines
* duplicate products
* invalid status transitions
* rejection reason

### Inventory

* reservation
* insufficient stock
* release
* commit
* invalid release
* no negative inventory

### Payment

* successful authorization
* rejected authorization
* duplicate authorization

---

# Integration Tests

Integration tests use Testcontainers where practical:

```text
SQL Server
RabbitMQ
Redis
```

Important scenarios:

```text
Duplicate HTTP idempotency key
Duplicate message delivery
Successful complete order
Insufficient stock
Payment rejection
Inventory compensation
Transient consumer failure
Retry behavior
Error queue behavior
Outbox atomicity
Concurrency conflict
No negative stock
No double reservation
No duplicate payment
```

The most important distributed workflow test is:

```text
Create Order
    ↓
Reserve Inventory
    ↓
Payment Rejected
    ↓
Release Inventory
    ↓
Order Rejected
```

---

# Docker Compose

Local development infrastructure includes:

```text
SQL Server
RabbitMQ
Redis
```

As the services become independently executable, Docker Compose may also run:

```text
Order API
Inventory Worker
Payment Worker
```

Docker Compose is for local development and CI.

It is not an SLA or production deployment architecture.

---

# Production Readiness

This project must not be described as production-ready merely because it uses:

* RabbitMQ
* MassTransit
* outbox
* retries
* Redis
* optimistic concurrency
* saga

Production-readiness requires evidence.

At minimum:

* meaningful integration tests
* failure-path tests
* concurrency tests
* observability
* benchmark evidence for performance claims
* configuration/secrets strategy
* deployment strategy
* operational documentation

No performance or reliability claim should be made without supporting evidence.

---

# Explicitly Later

The following are intentionally outside the current scope:

* real payment provider integration
* shipping service
* notification service
* multi-tenancy
* event sourcing
* CQRS read database
* Kubernetes
* distributed tracing beyond basic observability
* advanced fraud detection
* real payment settlement
* complex refund workflows

These can be considered only after the core distributed workflow is proven with integration tests.
