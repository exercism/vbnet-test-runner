FROM alpine:latest

RUN apk add bash icu-libs jq krb5-libs libgcc libintl libssl1.1 libstdc++ zlib wget 
RUN apk add libgdiplus --repository https://dl-3.alpinelinux.org/alpine/edge/testing/

ENV DOTNET_NOLOGO=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true

RUN mkdir -p /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet  && \
    wget https://dot.net/v1/dotnet-install.sh && \
    chmod +x dotnet-install.sh
RUN ./dotnet-install.sh -c 5.0 --install-dir /usr/share/dotnet
RUN ./dotnet-install.sh -c 6.0 --install-dir /usr/share/dotnet
RUN ./dotnet-install.sh -c 7.0 --install-dir /usr/share/dotnet

WORKDIR /tmp

# Pre-install packages for offline usage
RUN dotnet new console
RUN dotnet add package Microsoft.NET.Test.Sdk -v 16.8.3
RUN dotnet add package Microsoft.NET.Test.Sdk -v 17.4.1
RUN dotnet add package xunit -v 2.4.1
RUN dotnet add package xunit -v 2.4.2
RUN dotnet add package xunit.runner.visualstudio -v 2.4.3
RUN dotnet add package xunit.runner.visualstudio -v 2.4.5

WORKDIR /opt/test-runner
COPY . .
ENTRYPOINT ["/opt/test-runner/bin/run.sh"]
