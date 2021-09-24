FROM mcr.microsoft.com/dotnet/sdk:5.0.400-alpine3.13-amd64 AS build
WORKDIR /opt/test-runner

# Pre-install packages for offline usage
RUN dotnet new console --no-restore
RUN dotnet add package Microsoft.NET.Test.Sdk -v 16.8.3
RUN dotnet add package xunit -v 2.4.1
RUN dotnet add package xunit.runner.visualstudio -v 2.4.3
RUN dotnet add package Exercism.Tests -v 0.1.0-beta1

FROM mcr.microsoft.com/dotnet/sdk:5.0.400-alpine3.13-amd64 AS runtime
WORKDIR /opt/test-runner

RUN apk add bash jq

ENV DOTNET_NOLOGO=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true

COPY --from=build /root/.nuget/packages/ /root/.nuget/packages/
COPY . .
ENTRYPOINT ["/opt/test-runner/bin/run.sh"]
