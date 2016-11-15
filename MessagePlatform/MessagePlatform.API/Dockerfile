FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MessagePlatform.API/MessagePlatform.API.csproj", "MessagePlatform.API/"]
COPY ["MessagePlatform.Core/MessagePlatform.Core.csproj", "MessagePlatform.Core/"]
COPY ["MessagePlatform.Infrastructure/MessagePlatform.Infrastructure.csproj", "MessagePlatform.Infrastructure/"]
RUN dotnet restore "MessagePlatform.API/MessagePlatform.API.csproj"
COPY . .
WORKDIR "/src/MessagePlatform.API"
RUN dotnet build "MessagePlatform.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MessagePlatform.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MessagePlatform.API.dll"]