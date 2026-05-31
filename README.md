# ChatBot — Interview Simulator на .NET 10 + Semantic Kernel

## Стек

| Слой | Технология |
|------|-----------|
| Backend API | ASP.NET Core 10 Minimal API |
| LLM оркестрация | Semantic Kernel 1.x |
| LLM провайдер | OpenAI gpt-4o / Ollama (переключается конфигом) |
| Vector DB | Qdrant |
| БД | PostgreSQL 16 |
| Кэш/сессии | Redis 7 |
| Frontend | React 18 + TypeScript + Vite + Tailwind CSS |

## Быстрый старт

### 1. Настроить API ключ

Создать файл `ChatBot.Api/appsettings.Development.json` (в git не попадает):

```json
{
  "Llm": {
    "ApiKey": "sk-..."
  }
}
```

Для Ollama переключить провайдер в `appsettings.json`:
```json
{
  "Llm": {
    "Provider": "Ollama",
    "OllamaEndpoint": "http://localhost:11434",
    "OllamaModel": "llama3"
  }
}
```

### 2. Запустить backend

```
cd C:\_Proj\ChatBot\ChatBot.Api
dotnet run
```

API: `http://localhost:5249` · OpenAPI: `http://localhost:5249/openapi/v1.json`

### 3. Запустить frontend

```
cmd /c "cd /d C:\_Proj\ChatBot\chatbot-ui && npm install"
cmd /c "cd /d C:\_Proj\ChatBot\chatbot-ui && npm run dev"
```

UI: `http://localhost:5173`

### 4. Инфраструктура (опционально, нужна на этапе 4)

```
docker compose up -d
```

## API Endpoints

| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/api/chat/stream` | SSE-стриминг ответа (text/event-stream) |
| POST | `/api/documents/upload` | Загрузка PDF для RAG-индексации |

## Структура проекта

```
ChatBot/
├── ChatBot.Api/            # Minimal API — маршруты, DI
├── ChatBot.Core/           # Модели, интерфейсы (без зависимостей)
├── ChatBot.Infrastructure/ # ChatService (SK), RagService, IngestionService
├── chatbot-ui/             # React frontend
└── docker-compose.yml      # Qdrant + PostgreSQL + Redis
```

## Этапы разработки

- [x] Этап 1: Скаффолдинг проекта
- [x] Этап 2: React UI симулятора собеседований (🇷🇺/🇬🇧, SSE-стриминг, gpt-4o)
- [ ] Этап 3: Интеграционные тесты
- [ ] Этап 4: Ingestion pipeline (PdfPig → эмбеддинги → Qdrant)
- [ ] Этап 5: RAG-поиск в чате
