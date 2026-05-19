FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src/app

RUN mkdir ../TorProxy
COPY . ../TorProxy/

RUN dotnet new console --framework net9.0
RUN dotnet add reference ../TorProxy/src/TorProxy/TorProxy.csproj

COPY samples/Docker/Program.cs .

RUN dotnet build

FROM build AS publish
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=publish /app .

RUN cat /etc/os-release

RUN apt-get update -y && apt-get install -y --no-install-recommends libbrotli1 libmbedtls14 && apt-get clean && rm -rf /var/lib/apt/lists/*

RUN groupadd -r appgroup && useradd -r -g appgroup appuser
RUN chown -R appuser:appgroup /app
USER appuser

ENTRYPOINT ["dotnet", "app.dll"]
