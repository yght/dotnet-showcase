# MessagePlatform - Real-time Messaging System

A scalable real-time messaging platform built with .NET 8, SignalR, MongoDB, and Azure services.

## Features

- Real-time messaging with SignalR
- Group chat functionality
- File sharing capabilities
- User presence tracking
- Typing indicators
- Message delivery status
- Push notifications via Azure Notification Hub
- Scalable architecture with Redis for connection management

## Technology Stack

- **Backend**: .NET 8, ASP.NET Core
- **Real-time**: SignalR
- **Database**: MongoDB
- **Cache**: Redis
- **Message Queue**: Azure Service Bus
- **Authentication**: JWT Bearer
- **Container**: Docker

## Architecture

- Clean Architecture pattern
- Domain-Driven Design
- CQRS pattern with MediatR
- Repository pattern
- Dependency Injection

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker and Docker Compose
- MongoDB (or use Docker)
- Redis (or use Docker)

### Running with Docker

```bash
docker-compose up
```

### Running locally

1. Update connection strings in `appsettings.json`
2. Run MongoDB and Redis
3. Execute:
```bash
dotnet restore
dotnet run --project MessagePlatform.API
```

## API Endpoints

- `POST /api/auth/login` - User authentication
- `GET /api/messages/conversation/{userId}` - Get conversation history
- `GET /api/messages/group/{groupId}` - Get group messages
- `PUT /api/messages/{messageId}/read` - Mark message as read
- `DELETE /api/messages/{messageId}` - Delete message

## SignalR Hubs

- `/hubs/chat` - Main chat hub
  - `SendMessage` - Send direct message
  - `SendGroupMessage` - Send group message
  - `JoinGroup` - Join a group
  - `LeaveGroup` - Leave a group
  - `TypingIndicator` - Send typing status