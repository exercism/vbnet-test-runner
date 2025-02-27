FROM mcr.microsoft.com/dotnet/sdk:9.0.200-alpine3.21-amd64 AS build

WORKDIR /tmp

# Pre-install packages for offline usage
RUN dotnet new console && \
    dotnet add package Microsoft.NET.Test.Sdk -v 17.4.1 && \
    dotnet add package xunit -v 2.4.2 && \
    dotnet add package xunit.runner.visualstudio -v 2.4.5 && \
    dotnet add package Microsoft.NET.Test.Sdk -v 17.12.0 && \
    dotnet add package xunit -v 2.8.1 && \
    dotnet add package xunit.runner.visualstudio -v 3.0.1

# Build runtime image
FROM mcr.microsoft.com/dotnet/sdk:9.0.200-alpine3.21-amd64 AS runtime

RUN apk add --no-cache bash jq

WORKDIR /opt/test-runner

# Enable rolling forward the .NET SDK used to be backwards-compatible
ENV DOTNET_ROLL_FORWARD=Major

COPY --from=build /root/.nuget/packages/ /root/.nuget/packages/
COPY bin/ bin/

ENTRYPOINT ["/opt/test-runner/bin/run.sh"]
