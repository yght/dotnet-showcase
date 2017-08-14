# MessagePlatform API

[\![Build Status](https://dev.azure.com/messageplatform/MessagePlatform/_apis/build/status/MessagePlatform-CI?branchName=main)](https://dev.azure.com/messageplatform/MessagePlatform/_build/latest?definitionId=1&branchName=main)

Real-time messaging API built with .NET Core 1.1 and MongoDB.

## Features
- Send and receive messages
- Group messaging
- User presence tracking  
- Message search
- Push notifications
- Redis caching

## Quick Start
```bash
dotnet restore
dotnet run --project MessagePlatform.API
```

## API Documentation
Swagger UI available at `/swagger` when running in development mode.

## Testing
```bash
dotnet test MessagePlatform.Tests
```
