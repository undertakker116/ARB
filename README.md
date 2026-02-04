# ARB - Crypto Arbitrage Data Aggregator

ASP.NET Core приложение для сбора и агрегации данных о ценах криптовалют с централизованных и децентрализованных бирж.

## Что делает

Проект собирает данные о ценах криптовалют с 12 CEX бирж и OKX DEX, обрабатывает их и предоставляет через REST API. Основная задача - поиск арбитражных возможностей между биржами.

### Поддерживаемые биржи

**CEX (централизованные):**
- Binance
- Bybit
- OKX
- MEXC
- Bitget
- HTX (Huobi)
- Bitmart
- Gate.io
- KuCoin
- XT
- Lbank
- Poloniex

**DEX (децентрализованные):**
- OKX DEX (15 блокчейнов: Ethereum, Solana, Arbitrum, Base, BSC и др.)

### Основной функционал

1. **Сбор цен с CEX** - каждые 3 секунды опрашивает API бирж и кэширует данные
2. **Сбор цен с DEX** - получает цены токенов через OKX DEX API с данными о ликвидности и капитализации
3. **Агрегация данных** - объединяет цены с разных бирж для одного токена
4. **Проверка депозитов/выводов** - определяет доступность ввода/вывода на биржах
5. **Расчет комиссий** - собирает данные о комиссиях за вывод

## Запуск проекта

### Требования

- .NET 8.0 SDK
- macOS/Linux/Windows

### Установка и запуск

```bash
# Клонировать репозиторий
git clone <repo-url>
cd ARB

# Создать .env файл из примера
cp .env.example .env

# Заполнить .env файл своими API ключами
# Откройте .env в редакторе и добавьте ключи от бирж

# Восстановить зависимости
dotnet restore

# Запустить проект
dotnet run
```

Приложение запустится на `http://localhost:5000` (или порт из launchSettings.json).

### Настройка API ключей

Все API ключи хранятся в файле `.env` в корне проекта. Скопируйте `.env.example` в `.env` и заполните своими ключами:

```bash
# CoinGecko API (обязательно для DictService)
COINGECKO_API_KEY=your_key_here

# OKX API (обязательно для OkxDexService)
OKX_API_KEY=your_key_here
OKX_SECRET_KEY=your_secret_here
OKX_PASSPHRASE=your_passphrase_here

# Bybit API (опционально, для DictService)
BYBIT_API_KEY=your_key_here
BYBIT_SECRET_KEY=your_secret_here

# MEXC API (опционально, для DictService)
MEXC_API_KEY=your_key_here
MEXC_SECRET_KEY=your_secret_here
```

**Важно:** Файл `.env` добавлен в `.gitignore` и не попадет в git.

## API Endpoints (Swagger)

После запуска Swagger доступен по адресу: `http://localhost:5000/swagger`

### Основные эндпоинты

**Цены с CEX бирж:**
- `GET /api/binance_spot` - цены Binance
- `GET /api/bybit_spot` - цены Bybit
- `GET /api/okx_spot` - цены OKX
- `GET /api/mexc_spot` - цены MEXC
- `GET /api/bitget_spot` - цены Bitget
- `GET /api/htx_spot` - цены HTX
- `GET /api/bitmart_spot` - цены Bitmart
- `GET /api/gate_spot` - цены Gate.io
- `GET /api/kucoin_spot` - цены KuCoin
- `GET /api/xt_spot` - цены XT
- `GET /api/lbank_spot` - цены Lbank
- `GET /api/poloniex_spot` - цены Poloniex

**Агрегированные данные:**
- `GET /api/all_prices` - все цены со всех CEX бирж

**OKX DEX данные:**
- `GET /api/okx_dex` - полные данные с OKX DEX (цены, ликвидность, капитализация)
- `GET /api/okx_dex_log` - текущий лог работы OKX DEX сервиса
- `GET /api/okx_dex_log_files` - список файлов логов

**Словари (Dictionary):**
- `GET /api/dict` - основной словарь токенов
- `GET /api/dict_usdt` - токены с парами USDT
- `GET /api/dict_sol_eth` - токены с парами SOL/ETH
- `GET /api/dict_usdc` - токены с парами USDC

## Структура проекта

```
ARB/
├── Controllers/
│   └── DataController.cs          # REST API контроллер
├── Services/
│   ├── Cex/                       # Сервисы для CEX бирж
│   │   ├── BinanceService.cs
│   │   ├── BybitService.cs
│   │   └── ...                    # остальные биржи
│   ├── CexService.cs              # Агрегация данных с CEX
│   ├── OkxDexService.cs           # Работа с OKX DEX API
│   └── DictService.cs             # Построение словаря токенов
├── Models/
│   ├── MarketData.cs              # Модель данных токена
│   └── DictData.cs                # Модели для словарей
├── Input/
│   └── FinalDict.json             # Входной словарь токенов
├── Temp/                          # Временные файлы (запросы/ответы)
├── Logs/                          # Логи работы сервисов
└── Program.cs                     # Точка входа
```

## Как это работает

1. **Background Services** запускаются при старте приложения
2. Каждый CEX сервис опрашивает свою биржу каждые 3 секунды
3. Данные сохраняются в `IMemoryCache` с TTL 30 секунд
4. `CexService` читает данные из кэша и обновляет словарь токенов
5. `OkxDexService` получает цены DEX для токенов из `Input/FinalDict.json`
6. API контроллер отдает данные из кэша по запросу

## Конфигурация

### API ключи

Все API ключи настраиваются через переменные окружения в файле `.env`:

- `COINGECKO_API_KEY` - для получения данных о токенах и биржах
- `OKX_API_KEY`, `OKX_SECRET_KEY`, `OKX_PASSPHRASE` - для OKX DEX API
- `BYBIT_API_KEY`, `BYBIT_SECRET_KEY` - для получения данных о депозитах/выводах Bybit
- `MEXC_API_KEY`, `MEXC_SECRET_KEY` - для получения данных о депозитах/выводах MEXC

### Настройки батчинга

В `OkxDexService.cs`:
- `BatchSize = 100` - количество токенов в одном запросе
- `_delayBetweenRequests = 50` - задержка между запросами (мс)

## Логирование

Логи OKX DEX сервиса сохраняются в `Logs/` с ротацией каждый час.
Формат: `OkxDex_YYYY-MM-DD_HH-00-00.txt`

## Данные

Входной файл `Input/FinalDict.json` содержит список токенов с:
- Symbol (тикер)
- Chain (блокчейн)
- ContractAddress (адрес контракта)
- Exchanges (список бирж и торговых пар)

Результаты сохраняются в `Temp/okx_dex_prices.json` и `Temp/okx_dex_full_data.json`.
