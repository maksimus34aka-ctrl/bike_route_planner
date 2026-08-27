
# bike_route_planner.py
import argparse
import json
import math
import sys
import requests
from datetime import datetime
from colorama import init, Fore, Style

init(autoreset=True)

class RoutePlanner:
    def __init__(self, start, end, waypoints=None, profile="touring", color=False):
        self.start = self._parse_location(start)
        self.end = self._parse_location(end)
        self.waypoints = [self._parse_location(w) for w in waypoints] if waypoints else []
        self.profile = profile
        self.color = color and sys.stdout.isatty()
        self.route = None

    def _parse_location(self, loc):
        # Если координаты (lat,lng)
        if ',' in loc and not loc.replace(',','').replace('.','').replace('-','').isdigit():
            parts = loc.split(',')
            return {"lat": float(parts[0].strip()), "lng": float(parts[1].strip()), "name": loc}
        else:
            # Геокодинг (упрощённо - используем номинальные координаты для демо)
            return {"name": loc, "lat": 0, "lng": 0}

    def _geocode(self, name):
        # В реальном проекте использовать OSM Nominatim или Google Geocoding API
        # Для демонстрации возвращаем фиктивные координаты
        import hashlib
        h = int(hashlib.md5(name.encode()).hexdigest(), 16) % 360
        return {"lat": 50 + (h % 100) / 100, "lng": 10 + (h % 200) / 100}

    def calculate_route(self):
        # Если координаты не заданы, пытаемся геокодировать
        if self.start.get("lat") == 0 and self.start.get("lng") == 0:
            self.start = self._geocode(self.start["name"])
        if self.end.get("lat") == 0 and self.end.get("lng") == 0:
            self.end = self._geocode(self.end["name"])
        for w in self.waypoints:
            if w.get("lat") == 0 and w.get("lng") == 0:
                w.update(self._geocode(w["name"]))

        # Простой расчёт расстояния по прямой (для демонстрации)
        # В реальном проекте использовать OSRM API
        all_points = [self.start] + self.waypoints + [self.end]
        total_distance = 0
        for i in range(len(all_points)-1):
            p1 = all_points[i]
            p2 = all_points[i+1]
            dist = self._haversine(p1["lat"], p1["lng"], p2["lat"], p2["lng"])
            total_distance += dist
        # Время с учётом профиля
        speed = self._get_speed()
        time_hours = total_distance / speed if speed else 0
        # Набор высоты (упрощённо)
        elevation_gain = int(total_distance * 0.05 * 1000)  # 5% от дистанции в метрах

        self.route = {
            "start": self.start,
            "end": self.end,
            "waypoints": self.waypoints,
            "total_distance_km": round(total_distance, 2),
            "elevation_gain_m": elevation_gain,
            "time_hours": round(time_hours, 2),
            "profile": self.profile,
            "points": all_points
        }
        return self.route

    def _haversine(self, lat1, lon1, lat2, lon2):
        R = 6371.0
        phi1 = math.radians(lat1)
        phi2 = math.radians(lat2)
        dphi = math.radians(lat2 - lat1)
        dlambda = math.radians(lon2 - lon1)
        a = math.sin(dphi/2)**2 + math.cos(phi1)*math.cos(phi2)*math.sin(dlambda/2)**2
        return R * 2 * math.atan2(math.sqrt(a), math.sqrt(1-a))

    def _get_speed(self):
        speeds = {"road": 25, "mtb": 15, "touring": 20}
        return speeds.get(self.profile, 20)

    def print_route(self):
        if not self.route:
            self.calculate_route()
        r = self.route
        if self.color:
            print(Fore.CYAN + f"🚴 Маршрут от {self.start['name']} до {self.end['name']}")
            if self.waypoints:
                print(Fore.MAGENTA + f"   через {', '.join(w['name'] for w in self.waypoints)}")
            print(Fore.GREEN + f"Расстояние: {r['total_distance_km']:.1f} км")
            print(Fore.YELLOW + f"Набор высоты: {r['elevation_gain_m']} м")
            print(Fore.BLUE + f"Время в пути: ~{r['time_hours']:.1f} ч (при {self._get_speed()} км/ч)")
        else:
            print(f"Маршрут от {self.start['name']} до {self.end['name']}")
            if self.waypoints:
                print(f"   через {', '.join(w['name'] for w in self.waypoints)}")
            print(f"Расстояние: {r['total_distance_km']:.1f} км")
            print(f"Набор высоты: {r['elevation_gain_m']} м")
            print(f"Время в пути: ~{r['time_hours']:.1f} ч")

    def export_gpx(self, filename):
        if not self.route:
            self.calculate_route()
        gpx = '<?xml version="1.0" encoding="UTF-8"?>\n'
        gpx += '<gpx version="1.1" creator="BikeRoutePlanner">\n'
        gpx += '  <trk>\n    <trkseg>\n'
        for pt in self.route["points"]:
            gpx += f'      <trkpt lat="{pt["lat"]:.6f}" lon="{pt["lng"]:.6f}">\n'
            gpx += f'        <ele>{50 + int(pt["lat"] * 10)}</ele>\n'
            gpx += '      </trkpt>\n'
        gpx += '    </trkseg>\n  </trk>\n</gpx>'
        with open(filename, 'w') as f:
            f.write(gpx)
        print(f"GPX экспортирован в {filename}")

    def save_json(self, filename):
        if not self.route:
            self.calculate_route()
        with open(filename, 'w') as f:
            json.dump(self.route, f, indent=2)
        print(f"Маршрут сохранён в {filename}")

def main():
    parser = argparse.ArgumentParser(description="Планировщик велосипедных маршрутов")
    parser.add_argument("--start", required=True, help="Начальная точка (адрес или lat,lng)")
    parser.add_argument("--end", required=True, help="Конечная точка (адрес или lat,lng)")
    parser.add_argument("--waypoints", help="Промежуточные точки через запятую")
    parser.add_argument("--profile", choices=["road", "mtb", "touring"], default="touring", help="Профиль велосипедиста")
    parser.add_argument("--export-gpx", help="Экспорт в GPX")
    parser.add_argument("--save-json", help="Сохранить маршрут в JSON")
    parser.add_argument("--color", action="store_true", help="Принудительный цветной вывод")
    args = parser.parse_args()

    waypoints = args.waypoints.split(',') if args.waypoints else None
    planner = RoutePlanner(args.start, args.end, waypoints, args.profile, args.color)
    planner.calculate_route()
    planner.print_route()
    if args.export_gpx:
        planner.export_gpx(args.export_gpx)
    if args.save_json:
        planner.save_json(args.save_json)

if __name__ == "__main__":
    main()
