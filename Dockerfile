# Multi-stage сборка ChatBot.Api для деплоя на Railway.
# .NET: эквивалент publish-профиля + хостинг под Kestrel в Linux-контейнере.

# ---------- Stage 1: build & publish ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем csproj-файлы отдельно — для кэширования слоя restore
# .NET: dotnet restore по каждому проекту solution
COPY ChatBot.slnx ./
COPY ChatBot.Api/ChatBot.Api.csproj ChatBot.Api/
COPY ChatBot.Core/ChatBot.Core.csproj ChatBot.Core/
COPY ChatBot.Infrastructure/ChatBot.Infrastructure.csproj ChatBot.Infrastructure/

RUN dotnet restore ChatBot.Api/ChatBot.Api.csproj

# Копируем исходники и публикуем
COPY ChatBot.Api/ ChatBot.Api/
COPY ChatBot.Core/ ChatBot.Core/
COPY ChatBot.Infrastructure/ ChatBot.Infrastructure/
# Локали нужны в рантайме — кладём рядом с publish-артефактами
COPY locales/ locales/

RUN dotnet publish ChatBot.Api/ChatBot.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./
# Локали лежат в корне src, а не в publish — копируем явно
COPY --from=build /src/locales ./locales/

# Railway инжектирует $PORT динамически — Kestrel должен слушать его на 0.0.0.0.
# .NET: эквивалент app.Urls.Add($"http://*:{port}") в Program.cs.
# Shell-форма ENTRYPOINT нужна чтобы $PORT раскрылся в рантайме (exec-форма не делает expansion).
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet ChatBot.Api.dll
