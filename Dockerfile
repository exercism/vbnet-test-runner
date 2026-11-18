FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0.100-alpine3.24 AS build
ARG TARGETARCH

WORKDIR /tmp

# Pre-install packages for offline restores
RUN dotnet new console && \
    # .NET 10 support
    dotnet add package Exercism.Tests.xunit.v3 --version 0.1.0-beta1 && \
    dotnet add package Microsoft.NET.Test.Sdk -v 18.3.0 && \
    dotnet add package xunit.v3 -v 3.2.2 && \
    dotnet add package xunit.runner.visualstudio -v 3.1.5 && \
    # The save-the-cow exercise uses Microsoft.Reactive.Testing
    dotnet add package Microsoft.Reactive.Testing -v 5.0.0 && \
    # .NET 11 support
    dotnet add package Microsoft.NET.Test.Sdk -v 18.11.1 && \
    dotnet add package xunit.v3.mtp-off -v 4.0.3 && \
    dotnet add package xunit.runner.visualstudio -v 4.0.1

WORKDIR /app

# Override package location for runner-specific packages that are not needed in the final image.
ENV NUGET_PACKAGES=/tmp/runner-packages

# Copy csproj and restore as distinct layers
COPY src/Exercism.TestRunner.VBNet/Exercism.TestRunner.VBNet.vbproj ./
RUN dotnet restore -a $TARGETARCH

# Copy everything else and build
COPY src/Exercism.TestRunner.VBNet/ ./
RUN dotnet publish -a $TARGETARCH --output /opt/test-runner --no-restore

# Slim the pre-installed NuGet cache. The ReadyToRun compiler (crossgen2) and the runtime packs,
# restored for the runner, move to /r2r-packages, so that only the shape build below sees them.
RUN find /root/.nuget/packages -type f \( -name '*.nupkg' -o -name '*.snupkg' \) -delete && \
    mkdir /r2r-packages && \
    mv /tmp/runner-packages/microsoft.netcore.app.crossgen2.* \
       /tmp/runner-packages/microsoft.netcore.app.runtime.* \
       /tmp/runner-packages/microsoft.aspnetcore.app.runtime.* /r2r-packages/

# Build runtime image
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-alpine3.24 AS runtime

ENV DOTNET_ROLL_FORWARD=Major
ENV DOTNET_NOLOGO=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true

WORKDIR /opt/test-runner

COPY --from=build /opt/test-runner/ .
COPY --from=build /usr/local/bin/ /usr/local/bin/
COPY --from=build /root/.nuget/packages/ /root/.nuget/packages/

COPY bin/run.sh bin/

# Build a template project for each known project shape, for the runner's fast path
COPY shapes/ shapes/
COPY bin/build-shapes.sh bin/
RUN --mount=type=bind,from=build,source=/r2r-packages,target=/r2r-packages sh bin/build-shapes.sh

ENTRYPOINT ["sh", "/opt/test-runner/bin/run.sh"]
