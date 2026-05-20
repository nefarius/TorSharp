FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src/app

RUN mkdir ../TorProxy
COPY . ../TorProxy/

RUN dotnet new console --framework net10.0
RUN dotnet add reference ../TorProxy/src/TorProxy/TorProxy.csproj

COPY samples/Docker/Program.cs .

RUN dotnet build

FROM build AS publish
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=publish /app .

RUN cat /etc/os-release

RUN groupadd -r appgroup && useradd -r -g appgroup appuser
RUN chown -R appuser:appgroup /app
USER appuser

ENTRYPOINT ["dotnet", "app.dll"]
