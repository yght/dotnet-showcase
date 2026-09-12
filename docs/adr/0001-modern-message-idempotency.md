# Database-backed message idempotency

Author: Yousof

## Problem

A client cannot tell whether a timed-out POST failed before or after the message
was saved. Retrying without a durable identity can create duplicate messages.
A per-process dictionary loses that identity on restart and does not coordinate
multiple application instances.

## Decision

Use a unique `(tenant, sender, clientMessageId)` constraint in SQLite. Insert and
read the result inside one transaction. The same payload returns the original
record; changed content or recipient returns a conflict. Tenant and sender are
read from validated JWT claims rather than accepted in the request body.

Use an increasing database sequence for conversation pagination. Queries include
the tenant and both directions of the caller/peer relationship. The client advances
to the final returned sequence, not the current wall-clock time.

## Consequences

An ordinary retry is safe across connections and restarts. SQLite makes the
example reproducible without external infrastructure, but synchronous database
calls and serialized writers are deliberate scale limits. Receipt lifetime equals
message lifetime; retention changes must preserve the intended retry window.

This transaction does not include notifications or external effects. Adding them
would require an outbox and consumer deduplication. A cursor is an append traversal,
not a snapshot of the entire conversation, and exposed sequence values can have gaps.
