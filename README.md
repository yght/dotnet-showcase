# MessagePlatform API

A real-time messaging API built with .NET Core, inspired by messaging systems I've worked on professionally. This project demonstrates clean architecture patterns and API design principles I've used in production environments.

## What it does

- Send and receive messages between users
- Group messaging functionality  
- User presence tracking
- Message history and search
- Push notifications

## Tech Stack

- .NET Core 1.0 (November 2016 stack)
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