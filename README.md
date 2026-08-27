# Планировщик маршрутов (велосипед)

Многоязычная утилита для построения и оптимизации велосипедных маршрутов на основе OpenStreetMap (OSRM) или собственных вычислений.  
Поддерживает расчёт расстояния, времени в пути, набора высоты, рекомендации по покрытию и экспорт в GPX.

## Особенности
- Расчёт оптимального маршрута между несколькими точками (координаты или адреса).
- Учёт рельефа (набор высоты) и типа дорожного покрытия (асфальт, грунтовка).
- Оценка времени в пути с учётом средней скорости и уклона.
- Возможность задать промежуточные точки (waypoints).
- Экспорт маршрута в GPX-файл для навигационных устройств.
- Сохранение и загрузка маршрутов в JSON.
- Цветной вывод в терминале (где поддерживается).
- Поддержка аргументов командной строки для автоматизации.

## Установка и запуск
Для каждого языка требуются соответствующие инструменты и зависимости.

### Запуск на разных языках

1. **Python**  
   Установка: `pip install requests colorama` (для OSRM API, опционально).  
   Запуск: `python bike_route_planner.py --start "Москва" --end "Санкт-Петербург"`

2. **JavaScript (Node.js)**  
   Установка: `npm install axios commander chalk`  
   Запуск: `node bike_route_planner.js --start "Москва" --end "Санкт-Петербург"`

3. **Go**  
   Установка: модулей не требуется.  
   Запуск: `go run bike_route_planner.go --start "Москва" --end "Санкт-Петербург"`

4. **Rust**  
   Добавьте `clap`, `reqwest`, `serde`, `serde_json`, `chrono` в `Cargo.toml`.  
   Запуск: `cargo run -- --start "Москва" --end "Санкт-Петербург"`

5. **Java**  
   Используйте Gson и OkHttp.  
   Сборка: `javac -cp gson.jar:okhttp.jar BikeRoutePlanner.java`  
   Запуск: `java -cp .;gson.jar;okhttp.jar BikeRoutePlanner --start "Москва" --end "Санкт-Петербург"`

6. **C# (.NET Core)**  
   Установка: `dotnet add package Newtonsoft.Json`  
   Запуск: `dotnet run -- --start "Москва" --end "Санкт-Петербург"`

7. **C++ (Linux)**  
   Требуется libcurl, nlohmann/json.  
   Сборка: `g++ -std=c++11 -o bike_route_planner bike_route_planner.cpp -lcurl -ljsoncpp`  
   Запуск: `./bike_route_planner --start "Москва" --end "Санкт-Петербург"`

8. **Kotlin (JVM)**  
   Используйте Gson и OkHttp.  
   Сборка: `kotlinc -cp gson.jar:okhttp.jar BikeRoutePlanner.kt`  
   Запуск: `kotlin -cp .;gson.jar;okhttp.jar BikeRoutePlannerKt --start "Москва" --end "Санкт-Петербург"`

## Использование

Общие аргументы командной строки (везде, где поддерживается):

- `--start <адрес/коорд>` – начальная точка (обязательно).
- `--end <адрес/коорд>` – конечная точка (обязательно).
- `--waypoints <список>` – промежуточные точки через запятую.
- `--profile <тип>` – профиль велосипедиста: `road` (шоссе), `mtb` (горный), `touring` (туристический). По умолчанию `touring`.
- `--export-gpx <файл>` – экспорт маршрута в GPX.
- `--save-json <файл>` – сохранить маршрут в JSON.
- `--color` – принудительно включить цветной вывод.

Пример (Python):
```bash
python bike_route_planner.py --start "51.5074,-0.1278" --end "48.8566,2.3522" --waypoints "50.1109,8.6821" --profile road --export-gpx route.gpx
Пример вывода:

text
🚴 Маршрут от London до Paris (через Frankfurt)
Расстояние: 456.7 км
Набор высоты: 2345 м
Время в пути: ~22.8 ч (при скорости 20 км/ч)
Структура репозитория
text
/
├── README.md
├── bike_route_planner.py
├── bike_route_planner.js
├── bike_route_planner.go
├── bike_route_planner.rs
├── BikeRoutePlanner.java
├── BikeRoutePlanner.cs
├── bike_route_planner.cpp
└── BikeRoutePlanner.kt
Лицензия
MIT
