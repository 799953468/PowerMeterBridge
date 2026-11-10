# Build stage
FROM mcr.microsoft.com/dotnet/sdk:11.0-alpine AS build
WORKDIR /src

# Copy project files
COPY src/PowerMeterBridge/PowerMeterBridge.csproj src/PowerMeterBridge/
COPY appsettings.json.example .
RUN dotnet restore src/PowerMeterBridge/PowerMeterBridge.csproj

# Native AOT toolchain for Alpine Linux targets
RUN apk add --no-cache clang build-base zlib-dev

# Copy source code
COPY . .

# Build and publish AOT binary
# Multi-architecture support: set via --build-arg TARGETARCH
ARG TARGETARCH
RUN case "$TARGETARCH" in \
      "amd64") RUNTIME_ID=linux-musl-x64 ;; \
      "arm64") RUNTIME_ID=linux-musl-arm64 ;; \
      *) echo "Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac && \
    dotnet publish src/PowerMeterBridge/PowerMeterBridge.csproj -c Release \
      -r $RUNTIME_ID \
      --self-contained \
      -o /app/publish \
      -p:PublishAot=true \
      -p:StripSymbols=true \
      -p:IlcOptimizationPreference=Speed

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-alpine
WORKDIR /app

# Install required runtime dependencies for AOT binaries
RUN apk add --no-cache \
    icu-libs \
    libgcc \
    libstdc++ \
    zlib

# Create non-root user
RUN addgroup -g 1000 appuser && \
    adduser -u 1000 -G appuser -s /bin/sh -D appuser && \
    mkdir -p /app/data && \
    chown -R appuser:appuser /app

# Copy published binary
COPY --from=build --chown=appuser:appuser /app/publish .

# Copy configuration template
COPY --chown=appuser:appuser appsettings.json.example /app/

# Switch to non-root user
USER appuser

# Set environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0

# Health check (verify process is running)
HEALTHCHECK --interval=60s --timeout=10s --start-period=30s --retries=3 \
  CMD pgrep -f PowerMeterBridge || exit 1

# Default polling interval: 60 seconds
ENTRYPOINT ["./PowerMeterBridge"]
CMD ["60"]
