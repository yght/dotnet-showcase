# MessagePlatform API

A real-time messaging API built with .NET Core, inspired by messaging systems I've worked on professionally. This project demonstrates clean architecture patterns and API design principles I've used in production environments.

## What I want to demonstrate

I want to show how I structure a messaging backend in C#, with clear boundaries between HTTP endpoints, business services and data access.

- **API design:** direct and group messaging, conversation queries and presence endpoints.
- **Separation of concerns:** API, Core and Infrastructure projects.
- **Data-access decisions:** MongoDB queries, indexes and conversation-cache invalidation.
- **Customer experience:** message history, unread state and notifications as parts of a usable messaging product.

**Start here:** [messages controller](MessagePlatform/MessagePlatform.API/Controllers/MessagesController.cs), [message service](MessagePlatform/MessagePlatform.Infrastructure/Services/MessageService.cs), and [polling controller](MessagePlatform/MessagePlatform.API/Controllers/PollingController.cs).

**Scope:** a historical portfolio sample inspired by professional work. The API project targets `netcoreapp1.1`; this snapshot does not demonstrate a completed migration to .NET 6 or a current .NET stack. The tests currently contain placeholder assertions and do not establish correctness. Build compatibility and runtime integration need validation.

**Related samples:** [message-web](https://github.com/yght/message-web) explores client state and [message-azure](https://github.com/yght/message-azure) explores notification policy and infrastructure. These snapshots are not integrated end to end: the React client expects different payload fields and client-message IDs that this API does not implement.

## What it does

- Send and receive messages between users
- Group messaging functionality  
- User presence tracking
- Message history and search
- Push notifications

## Tech Stack

- .NET Core 1.1 target (historical API sample)
- ASP.NET Core Web API
- MongoDB for data storage
- JWT authentication
- AutoMapper for object mapping
- FluentValidation for input validation

## Why these choices?

I built this using the 2016 .NET Core stack to show how messaging APIs were architected before SignalR Core existed. Back then we relied on HTTP polling and push notifications instead of WebSockets - which actually worked pretty well for most use cases.

The clean architecture separation (Core/Infrastructure/API) is something I always push for in team projects. Makes testing easier and keeps business logic separate from framework concerns.

## Running it

```bash
git clone https://github.com/yght/dotnet-showcase.git
cd dotnet-showcase/MessagePlatform
dotnet restore
dotnet run --project MessagePlatform.API
```

You'll need MongoDB running locally or update the connection string in appsettings.json.

## API Endpoints

- `POST /api/messages` - Send a message
- `GET /api/messages/conversations/{userId}` - Get conversation history
- `GET /api/polling/messages` - Poll for new messages
- `POST /api/polling/heartbeat` - Update user presence

## Notes

This is portfolio code - not production ready. Missing things like rate limiting, proper error handling, and security hardening that you'd want in a real system.

Built this to demonstrate API design patterns I've used professionally while keeping the code clean and readable.