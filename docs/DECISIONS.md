# Architectural Decisions

Numbered architectural decisions for OrderFlow.NET.

These records explain **why** a decision was made, not how a feature works as a tutorial.

---

## 1. Three business services, separate processes

**Decision:** OrderFlow.NET uses three independent business services:

* `OrderFlow.Order`
* `OrderFlow.Inventory`
* `OrderFlow.Payment`

Each service runs as an independent process.

**Why:** The purpose of the project is to demonstrate meaningful distributed-system boundaries. Order, inventory, and payment have different responsibilities, consistency requirements, and failure modes. Separate processes make those boundaries explicit without creating an unnecessarily large microservice ecosystem.

---

## 2. Database-per-service

**Decision:** Each service owns its own database.

```text
Order Service     → OrderDb
Inventory Service → InventoryDb
Payment Service   → PaymentDb
```

**Why:** Service ownership is incomplete if services directly share and modify the same database. Database-per-service forces communication through explicit contracts and makes eventual consistency and failure handling real architectural concerns.

---

## 3. RabbitMQ for asynchronous service communication

**Decision:** RabbitMQ is the transport for asynchronous communication between services.

MassTransit manages the messaging infrastructure.

**Why:** Order processing does not require synchronous coupling between services. RabbitMQ provides an explicit asynchronous boundary and allows services to fail independently.

---

## 4. MassTransit for messaging infrastructure

**Decision:** MassTransit is responsible for:

* consumers
* retries
* endpoint configuration
* message topology
* EF Core outbox
* inbox/idempotency
* error queues
* saga state machine integration

**Why:** Implementing these mechanisms manually would add infrastructure code without providing meaningful learning value for this project. MassTransit provides established implementations while leaving the business logic inside the application/domain layers.

---

## 5. Transactional outbox instead of direct publish-after-save

**Decision:** Services use MassTransit's EF Core transactional outbox when a business transaction also produces an integration message.

**Why:** Directly performing:

```text
Save database
Publish RabbitMQ message
```

creates a dual-write failure window.

The business change and outbox record must instead commit together:

```text
Business data
+
Outbox message
        ↓
Same SQL transaction
```

The outbox dispatcher publishes the message after the transaction commits.

---

## 6. RabbitMQ is not part of the SQL transaction

**Decision:** RabbitMQ is not treated as a transactional participant in SQL Server transactions.

**Why:** RabbitMQ does not provide SQL-style rollback semantics. The transactional boundary is the service's local database. The outbox bridges the local transaction to asynchronous message delivery.

If the SQL transaction rolls back, the outbox message is rolled back with it.

If RabbitMQ delivery fails after the database commit, the outbox message remains available for later delivery.

---

## 7. Inbox/idempotent consumers

**Decision:** Consumers use MassTransit's inbox/idempotency mechanism.

**Why:** At-least-once message delivery means duplicate delivery must be expected.

This is particularly important for inventory and payment operations.

The system must not:

```text
receive OrderCreated twice
→ reserve stock twice
```

or:

```text
receive payment authorization twice
→ create two successful payments
```

---

## 8. Eventual consistency instead of distributed transactions

**Decision:** Order, Inventory, and Payment do not participate in a distributed ACID transaction.

**Why:** Each service owns its database. Cross-service consistency is achieved through asynchronous events and commands.

The system therefore accepts temporary intermediate states.

For example:

```text
Order = InventoryReserved
Payment = Pending
```

is a valid temporary state.

The workflow eventually reaches:

```text
Confirmed
```

or:

```text
Rejected
```

---

## 9. Saga State Machine for workflow orchestration

**Decision:** The distributed order workflow is coordinated using a MassTransit Saga State Machine.

**Why:** Once inventory and payment become independent services, the workflow contains multiple steps and a compensation path.

The workflow is no longer just:

```text
A → B → C
```

It becomes:

```text
Reserve Inventory
       ↓
Authorize Payment
       ↓
Confirm Order

Payment failure
       ↓
Release Inventory
       ↓
Reject Order
```

A saga provides explicit workflow state and compensation without requiring a distributed transaction.

---

## 10. Saga coordinates; domains enforce business rules

**Decision:** The saga orchestrates the workflow but does not contain domain business rules.

**Why:** Putting inventory or payment rules inside the saga would create a distributed god object.

The boundaries remain:

```text
Order Service
    → Order rules

Inventory Service
    → Inventory rules

Payment Service
    → Payment rules

Saga
    → Workflow coordination
```

---

## 11. Choreography is not the primary workflow mechanism

**Decision:** The project uses explicit saga orchestration for the main order workflow rather than relying entirely on event choreography.

**Why:** With only two steps, choreography is simple. With inventory reservation, payment authorization, confirmation, and compensation, purely event-driven choreography becomes harder to reason about.

The saga makes the workflow state and compensation path explicit.

Events remain the integration mechanism; the saga coordinates the business process.

---

## 12. Compensation instead of rollback across services

**Decision:** Cross-service failures are handled through compensating actions.

**Why:** A transaction cannot be rolled back across independent databases.

Example:

```text
InventoryReserved
        ↓
PaymentRejected
        ↓
ReleaseInventory
```

The system does not attempt to undo a committed inventory transaction through database rollback. It creates a new business operation that compensates the previous operation.

---

## 13. Optimistic concurrency for inventory

**Decision:** Inventory uses SQL Server optimistic concurrency with `rowversion`.

**Why:** Inventory correctness is a database consistency problem. Distributed Redis locks would introduce another coordination mechanism and another source of failure.

Example:

```text
Stock = 5

Order A → reserve 4
Order B → reserve 4
```

One update wins.

The conflicting transaction receives a concurrency exception and retries against current state.

The second request then sees insufficient stock.

---

## 14. Inventory has available and reserved quantities

**Decision:** Inventory tracks both:

```text
AvailableQuantity
ReservedQuantity
```

**Why:** Inventory must support compensation.

If stock is immediately destroyed from `AvailableQuantity`, payment failure cannot cleanly release the reservation.

Reservation:

```text
Available -= quantity
Reserved += quantity
```

Release:

```text
Available += quantity
Reserved -= quantity
```

Successful payment:

```text
Reserved -= quantity
```

This makes reservation lifecycle explicit.

---

## 15. Redis is only HTTP idempotency infrastructure

**Decision:** Redis is used only for HTTP-level `Idempotency-Key` handling.

**Why:** HTTP retries and message retries are different problems.

Redis protects:

```text
Client
  ↓
POST /orders
  ↓
duplicate HTTP request
```

MassTransit inbox protects:

```text
RabbitMQ
  ↓
duplicate message
  ↓
consumer
```

Redis is intentionally not used for:

* inventory locking
* distributed mutexes
* payment state
* order source of truth
* message idempotency

---

## 16. Redis failure is fail-closed for order creation

**Decision:** If Redis is unavailable, `POST /api/orders` fails with `503 Service Unavailable`.

**Why:** Without the idempotency store, the API cannot safely guarantee the requested HTTP idempotency contract.

Creating the order anyway would silently weaken the API's correctness guarantee.

---

## 17. Shared contracts, not shared domain models

**Decision:** Services share only `OrderFlow.Contracts`.

They do not reference another service's Domain, Application, or Infrastructure project.

**Why:** Sharing domain entities between services creates tight coupling and undermines service ownership.

The shared contract represents communication, not internal implementation.

---

## 18. Integration messages are explicit contracts

**Decision:** Commands and events are represented as explicit message contracts.

Examples:

```text
OrderCreated
ReserveInventory
InventoryReserved
InventoryRejected
ReleaseInventory
InventoryReleased
AuthorizePayment
PaymentAuthorized
PaymentRejected
```

**Why:** Message intent must be visible at the architectural boundary.

Generic messages such as:

```text
UpdateEntity
ProcessOrder
DoSomething
```

hide business intent and make distributed workflows difficult to understand.

---

## 19. MediatR is used for in-process application dispatching

**Decision:** MediatR is used inside services for commands and queries.

Pipeline behaviors are used for cross-cutting concerns such as:

* logging
* performance measurement
* validation where appropriate

**Why:** The application layer benefits from explicit command/query boundaries and pipeline behaviors.

MediatR is an in-process abstraction.

It is not used as the distributed messaging mechanism.

RabbitMQ/MassTransit remains responsible for cross-process communication.

---

## 20. CQRS without a separate read database

**Decision:** CQRS separates command and query responsibilities, but both use the service's primary database.

**Why:** A second read store would add complexity without providing meaningful value for the current project.

CQRS here means:

```text
Command model ≠ Query responsibility
```

It does not require:

```text
Write DB ≠ Read DB
```

A separate read model can be introduced later if a real requirement emerges.

---

## 21. No generic repository

**Decision:** Repositories are use-case/domain-oriented rather than generic CRUD abstractions.

**Why:** Generic repositories often hide the actual business operations and provide little value over EF Core.

Prefer intent-revealing operations such as:

```text
ReserveStock
ReleaseStock
CommitReservation
```

over generic methods such as:

```text
Update
Save
```

when the operation represents a meaningful business action.

---

## 22. Payment is simulated initially

**Decision:** Payment authorization is simulated inside the Payment service.

No real payment provider is required for the initial implementation.

**Why:** The architectural goal is to demonstrate distributed workflow, failure handling, idempotency, and compensation.

Introducing Stripe, Adyen, or another provider would add external credentials, network failure modes, and provider-specific complexity before the core architecture has been proven.

A real provider can be introduced later behind a payment abstraction.

---

## 23. Payment authorization must be idempotent

**Decision:** A payment authorization request must not create multiple successful payment records for the same logical operation.

**Why:** Messaging uses at-least-once delivery and payment is a high-risk operation for duplicate processing.

A duplicate:

```text
AuthorizePayment
```

must not result in:

```text
Charge #1
Charge #2
```

in a future real-payment implementation.

---

## 24. Business failures are not infrastructure retries

**Decision:** Business outcomes such as insufficient inventory or payment rejection are represented as explicit business events.

They are not retried indefinitely as technical failures.

**Why:** A retry cannot make insufficient stock appear or turn a declined payment into a successful one.

Technical/transient failures:

```text
SQL timeout
temporary network failure
broker connectivity problem
```

may be retried.

Business failures:

```text
InsufficientStock
PaymentRejected
InvalidBusinessState
```

should produce explicit outcomes.

---

## 25. Retry policy is for transient failures

**Decision:** MassTransit retry policies use bounded retries with exponential backoff for transient technical failures.

**Why:** Infinite retries can create poison-message loops and hide persistent failures.

After retry exhaustion, the message is moved to the configured error queue.

Operational recovery can then inspect and replay the message where appropriate.

---

## 26. Worker/service migration ownership

**Decision:** Each service owns and applies its own EF Core migrations.

The Order, Inventory, and Payment services must not attempt to migrate another service's database.

**Why:** Database ownership follows service ownership.

This prevents cross-service deployment coupling and migration races.

---

## 27. Domain layer remains infrastructure-free

**Decision:** Domain projects must not reference:

* EF Core
* ASP.NET Core
* MassTransit
* RabbitMQ
* Redis
* MediatR

**Why:** Domain rules should remain independently testable and independent from infrastructure technology.

Infrastructure adapters implement persistence and messaging concerns outside the domain.

---

## 28. Evidence before production-readiness claims

**Decision:** The project will not be described as production-ready until the distributed failure paths are demonstrated by tests.

**Why:** Having an outbox, retry policy, and saga in configuration does not prove correctness.

Evidence must include tests for:

* duplicate messages
* outbox atomicity
* retry behavior
* payment failure compensation
* concurrent stock reservation
* no negative inventory
* idempotent payment authorization
* eventual order completion

Performance claims also require benchmarks.

---

## 29. Keep the system intentionally small

**Decision:** The initial architecture contains only:

```text
Order
Inventory
Payment
```

No separate:

```text
Shipping
Notification
Catalog
User
Fraud
Gateway
```

services are introduced without a concrete architectural reason.

**Why:** The goal is to demonstrate distributed architecture, not maximize the number of services.

Every new service must introduce a meaningful ownership or consistency boundary.

---

## 30. Architecture evolves through evidence

**Decision:** Architectural complexity is introduced incrementally.

The project should first prove:

```text
Order
 ↓
Inventory
 ↓
Payment
 ↓
Confirmation
```

and:

```text
Payment failure
 ↓
Inventory compensation
 ↓
Rejection
```

before adding additional distributed concerns.

**Why:** Distributed systems become difficult because of interactions between failure modes, not because of the number of classes.

The architecture should therefore grow only when a concrete requirement justifies the additional coordination.
