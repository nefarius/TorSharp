FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src/app

RUN mkdir ../TorProxy
COPY . ../TorProxy/

RUN dotnet new console --framework net9.0
RUN dotnet add reference ../TorProxy/src/TorProxy/TorProxy.csproj

COPY samples/Docker-Alpine/Program.cs .

RUN dotnet build

FROM build AS publish
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine
WORKDIR /app
COPY --from=publish /app .

RUN cat /etc/os-release

RUN apk add --no-cache tor

RUN addgroup -S appgroup && adduser -S appuser -G appgroup
RUN chown -R appuser:appgroup /app
USER appuser

ENTRYPOINT ["dotnet", "app.dll"]
