# Messaging architecture in C# — Yousof

A portfolio of messaging design decisions, with a runnable **.NET 10 API** and an
older .NET Core sample retained for context. Start with the modern implementation.

## What I want to demonstrate

- **Reliable APIs:** a retry with the same client message ID returns the original message; changed content conflicts.
- **Data boundaries:** tenant and sender identity come from verified JWT claims, and conversation reads are restricted to the caller's participation.
- **Durability:** SQLite uniqueness and a transaction protect idempotency across connections and process restarts.
- **Pagination:** an increasing sequence cursor avoids offset shifts and timestamp ties when messages arrive between reads.
- **Customer experience:** safe retries avoid duplicate messages after timeouts, while bounded reads keep conversation requests predictable.

## Five-minute review

| Read | What it shows |
|---|---|
| [API endpoints](modern/Messaging.Api/Program.cs) | JWT policy, validation, conflict responses and bounded queries |
| [Message store](modern/Messaging.Api/MessageStore.cs) | Transactional idempotency, tenant filters and sequence cursors |
| [Integration tests](modern/Messaging.Tests/ApiTests.cs) | Signed JWTs, concurrent retries, isolation and real SQLite storage |
| [Design decision](docs/adr/0001-modern-message-idempotency.md) | Why these boundaries were chosen and what they cost |

## Run the modern sample

Requires the .NET 10 SDK. From the repository root:

```bash
dotnet test modern/ModernMessaging.sln
cd modern/Messaging.Api
dotnet user-jwts create --name alice --claim tenant_id=demo --audience http://localhost:5080
ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://localhost:5080
```

The JWT tool stores the signing key in local user secrets and writes development
issuer/audience settings. Copy its token into `TOKEN` in a second terminal:

```bash
export TOKEN='<token from dotnet user-jwts>'
curl -i http://localhost:5080/api/messages/ \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"recipientId":"bob","body":"Hello","clientMessageId":"send-001"}'
curl -s http://localhost:5080/api/messages/conversations/bob \
  -H "Authorization: Bearer $TOKEN"
```

Repeat the POST: the first response is `201`, a replay is `200`, and both contain
the same message ID. Reusing `send-001` with a different body or recipient returns
`409`. Use `?after=<nextCursor>&limit=50` for the next page. A page with no new
messages keeps the existing cursor.

The database defaults to `messages.db` in the working directory. Set
`ConnectionStrings__Messages` to select a persistent local file. `/health` is an
unauthenticated liveness endpoint, not a database readiness check.

## Verification

[GitHub Actions](.github/workflows/modern-dotnet.yml) restores, builds with warnings
as errors, and runs the modern test suite. Tests use real bearer-token validation
and isolated SQLite files; no cloud subscription or running database is needed.

The suite includes concurrent duplicate sends, changed-payload conflicts, expired
and missing tokens, missing tenant claims, tenant and participant isolation,
restart recovery and cursor pagination. These checks establish sample behaviour;
they are not a throughput benchmark or a production security audit.

## Scope and next steps

This is a new direct-message implementation. The [historical source](MessagePlatform/)
shows the older API/Core/Infrastructure structure; its placeholder tests are not
part of the modern solution. This is not a completed migration of that application.

SQLite uses synchronous I/O and serializes writers. A busy store can block request
threads. The next scaling step would be an asynchronous server database, with the
same uniqueness and transaction guarantees verified by integration tests.

For deployment, configure a trusted identity provider, token claims, TLS, rate
limits, recipient membership checks, retention and schema migrations. The API
accepts recipient identifiers but does not maintain a tenant user directory.
Notification delivery, groups, search, WebSockets and a client adapter are outside
this slice. The related [React client](https://github.com/yght/message-web) and
[Azure notification sample](https://github.com/yght/message-azure) illustrate
adjacent concerns and do not yet form an integrated application with this API.

No measured production scale or performance gain is claimed for this sample.

## Engineering practices

[Contribution and verification guide](CONTRIBUTING.md) · [Review template](.github/pull_request_template.md)

The modern .NET 10 solution is the verification target. Historical MessagePlatform source is not included in that gate.

## Operational readiness

- `GET /health` reports process liveness.
- `GET /health/ready` opens SQLite read-only and checks the message schema: 200 when accessible, 503 when storage is unavailable. Responses disable caching and omit database error details.
- Configure traffic routing to use readiness, and restart monitoring to use liveness. A database outage should remove traffic without causing a restart loop.
- This probe does not create missing databases or write test messages. It proves read access and schema presence, not disk capacity, write permissions or external-service health. The SQLite lock timeout is one second; this is not an end-to-end request deadline.

The modern suite now includes storage-failure and missing-database regression tests. Review `CheckReadiness` and the health endpoint integration test to discuss the difference between a live process and a service ready for traffic.
