FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.302-alpine3.23 AS build
ARG TARGETARCH

WORKDIR /tmp

# Pre-install packages for offline restores
RUN dotnet new console && \
    # .NET 9 support
    dotnet add package Exercism.Tests --version 0.1.0-beta1 && \
    dotnet add package Microsoft.NET.Test.Sdk -v 17.12.0 && \
    dotnet add package xunit -v 2.8.1 && \
    dotnet add package xunit.runner.visualstudio -v 3.0.1 && \
    # .NET 10 support
    dotnet add package Exercism.Tests.xunit.v3 --version 0.1.0-beta1 && \
    dotnet add package Microsoft.NET.Test.Sdk -v 18.3.0 && \
    dotnet add package xunit.v3 -v 3.2.2 && \
    dotnet add package xunit.runner.visualstudio -v 3.1.5

WORKDIR /app

# Override package location for runner-specific packages that are not needed in the final image.
ENV NUGET_PACKAGES=/tmp/runner-packages

# Copy csproj and restore as distinct layers
COPY src/Exercism.TestRunner.VBNet/Exercism.TestRunner.VBNet.vbproj ./
RUN dotnet restore -a $TARGETARCH

# Copy everything else and build
COPY src/Exercism.TestRunner.VBNet/ ./
RUN dotnet publish -a $TARGETARCH --output /opt/test-runner --no-restore

# Slim the pre-installed NuGet cache
RUN find /root/.nuget/packages -type f \( -name '*.nupkg' -o -name '*.snupkg' \) -delete

# Build runtime image
FROM mcr.microsoft.com/dotnet/sdk:10.0.302-alpine3.23 AS runtime

ENV DOTNET_ROLL_FORWARD=Major
ENV DOTNET_NOLOGO=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true

WORKDIR /opt/test-runner

COPY --from=build /opt/test-runner/ .
COPY --from=build /usr/local/bin/ /usr/local/bin/
COPY --from=build /root/.nuget/packages/ /root/.nuget/packages/

COPY bin/run.sh bin/

ENTRYPOINT ["sh", "/opt/test-runner/bin/run.sh"]
