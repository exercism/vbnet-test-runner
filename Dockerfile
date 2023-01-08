FROM alpine:latest

RUN apk add bash icu-libs krb5-libs libgcc libintl libssl1.1 libstdc++ zlib wget 
RUN apk add libgdiplus --repository https://dl-3.alpinelinux.org/alpine/edge/testing/

RUN mkdir -p /usr/share/dotnet \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet  && \
    wget https://dot.net/v1/dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh -c 5.0 --install-dir /usr/share/dotnet && \
    ./dotnet-install.sh -c 6.0 --install-dir /usr/share/dotnet && \
    ./dotnet-install.sh -c 6.0 --install-dir /usr/share/dotnet && \
    apk add jq && \
    dotnet new
    
WORKDIR /opt/test-runner
ENV DOTNET_NOLOGO=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true
COPY . .
ENTRYPOINT ["/opt/test-runner/bin/run.sh"]
