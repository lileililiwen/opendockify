# syntax=docker/dockerfile:1
# Multi-stage build: sdk -> publish single-file -> aspnet runtime with CJK fonts.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore OpenDockify.sln

RUN dotnet publish src/OpenDockify.Api/OpenDockify.Api.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o /app/publish

# Runtime stage. QuestPDF requires CJK fonts to render Chinese; stock images
# lack them, so install fonts-noto-cjk here (see Agents.md pitfalls).
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-noto-cjk ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# SQLite database + generated PDFs live under /app/data (Docker volume).
VOLUME ["/app/data"]
ENV ConnectionStrings__Default="Data Source=/app/data/opendockify.db"

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -fsS http://localhost:8080/healthz || exit 1

ENTRYPOINT ["./OpenDockify.Api"]
