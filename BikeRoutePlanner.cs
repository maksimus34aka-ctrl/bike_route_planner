// BikeRoutePlanner.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BikeRoutePlanner
{
    class Program
    {
        static void Main(string[] args)
        {
            var opts = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--start": opts.Start = args[++i]; break;
                    case "--end": opts.End = args[++i]; break;
                    case "--waypoints": opts.Waypoints = args[++i]; break;
                    case "--profile": opts.Profile = args[++i]; break;
                    case "--export-gpx": opts.GpxFile = args[++i]; break;
                    case "--save-json": opts.JsonFile = args[++i]; break;
                    case "--color": opts.Color = true; break;
                }
            }
            var planner = new Planner(opts);
            planner.Run();
        }

        class Options
        {
            public string Start { get; set; }
            public string End { get; set; }
            public string Waypoints { get; set; }
            public string Profile { get; set; } = "touring";
            public string GpxFile { get; set; }
            public string JsonFile { get; set; }
            public bool Color { get; set; }
        }

        class Point
        {
            public double Lat { get; set; }
            public double Lng { get; set; }
            public string Name { get; set; }
        }

        class Route
        {
            public Point Start { get; set; }
            public Point End { get; set; }
            public List<Point> Waypoints { get; set; }
            public double TotalDistanceKm { get; set; }
            public int ElevationGainM { get; set; }
            public double TimeHours { get; set; }
            public string Profile { get; set; }
            [JsonIgnore]
            public List<Point> Points { get; set; }
        }

        class Planner
        {
            private Options opts;
            private bool color;

            public Planner(Options opts)
            {
                this.opts = opts;
                this.color = opts.Color || !Console.IsOutputRedirected;
            }

            private Point ParseLocation(string loc)
            {
                if (loc.Contains(","))
                {
                    var parts = loc.Split(',');
                    if (double.TryParse(parts[0].Trim(), out double lat) && double.TryParse(parts[1].Trim(), out double lng))
                        return new Point { Lat = lat, Lng = lng, Name = loc };
                }
                return new Point { Lat = 0, Lng = 0, Name = loc };
            }

            private Point Geocode(string name)
            {
                int hash = Math.Abs(name.GetHashCode()) % 360;
                return new Point
                {
                    Lat = 50 + (hash % 100) / 100.0,
                    Lng = 10 + (hash % 200) / 100.0,
                    Name = name
                };
            }

            private double Haversine(double lat1, double lon1, double lat2, double lon2)
            {
                const double R = 6371.0;
                double phi1 = lat1 * Math.PI / 180;
                double phi2 = lat2 * Math.PI / 180;
                double dphi = (lat2 - lat1) * Math.PI / 180;
                double dlambda = (lon2 - lon1) * Math.PI / 180;
                double a = Math.Sin(dphi / 2) * Math.Sin(dphi / 2) +
                           Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(dlambda / 2) * Math.Sin(dlambda / 2);
                return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            }

            private double GetSpeed()
            {
                return opts.Profile switch
                {
                    "road" => 25,
                    "mtb" => 15,
                    _ => 20
                };
            }

            public Route CalculateRoute()
            {
                var start = ParseLocation(opts.Start);
                var end = ParseLocation(opts.End);
                var wayPts = new List<Point>();
                if (!string.IsNullOrEmpty(opts.Waypoints))
                {
                    foreach (var w in opts.Waypoints.Split(','))
                        wayPts.Add(ParseLocation(w.Trim()));
                }
                if (start.Lat == 0) start = Geocode(start.Name);
                if (end.Lat == 0) end = Geocode(end.Name);
                foreach (var w in wayPts)
                    if (w.Lat == 0) { var g = Geocode(w.Name); w.Lat = g.Lat; w.Lng = g.Lng; }
                var points = new List<Point> { start };
                points.AddRange(wayPts);
                points.Add(end);
                double totalDist = 0;
                for (int i = 0; i < points.Count - 1; i++)
                    totalDist += Haversine(points[i].Lat, points[i].Lng, points[i + 1].Lat, points[i + 1].Lng);
                double speed = GetSpeed();
                double timeHours = totalDist / speed;
                int elevGain = (int)(totalDist * 0.05 * 1000);
                return new Route
                {
                    Start = start,
                    End = end,
                    Waypoints = wayPts,
                    TotalDistanceKm = totalDist,
                    ElevationGainM = elevGain,
                    TimeHours = timeHours,
                    Profile = opts.Profile,
                    Points = points
                };
            }

            public void PrintRoute(Route r)
            {
                if (color)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"🚴 Маршрут от {r.Start.Name} до {r.End.Name}");
                    if (r.Waypoints.Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"   через {string.Join(", ", r.Waypoints.Select(w => w.Name))}");
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Расстояние: {r.TotalDistanceKm:F1} км");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Набор высоты: {r.ElevationGainM} м");
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"Время в пути: ~{r.TimeHours:F1} ч");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine($"Маршрут от {r.Start.Name} до {r.End.Name}");
                    if (r.Waypoints.Any())
                        Console.WriteLine($"   через {string.Join(", ", r.Waypoints.Select(w => w.Name))}");
                    Console.WriteLine($"Расстояние: {r.TotalDistanceKm:F1} км");
                    Console.WriteLine($"Набор высоты: {r.ElevationGainM} м");
                    Console.WriteLine($"Время в пути: ~{r.TimeHours:F1} ч");
                }
            }

            public void ExportGPX(Route r, string filename)
            {
                var gpx = new System.Text.StringBuilder();
                gpx.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                gpx.AppendLine("<gpx version=\"1.1\" creator=\"BikeRoutePlanner\">");
                gpx.AppendLine("  <trk>");
                gpx.AppendLine("    <trkseg>");
                foreach (var pt in r.Points)
                {
                    gpx.AppendLine($"      <trkpt lat=\"{pt.Lat:F6}\" lon=\"{pt.Lng:F6}\">");
                    gpx.AppendLine($"        <ele>{(int)(50 + pt.Lat * 10)}</ele>");
                    gpx.AppendLine("      </trkpt>");
                }
                gpx.AppendLine("    </trkseg>");
                gpx.AppendLine("  </trk>");
                gpx.AppendLine("</gpx>");
                File.WriteAllText(filename, gpx.ToString());
                Console.WriteLine($"GPX экспортирован в {filename}");
            }

            public void SaveJSON(Route r, string filename)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(r, options);
                File.WriteAllText(filename, json);
                Console.WriteLine($"Маршрут сохранён в {filename}");
            }

            public void Run()
            {
                var route = CalculateRoute();
                PrintRoute(route);
                if (!string.IsNullOrEmpty(opts.GpxFile)) ExportGPX(route, opts.GpxFile);
                if (!string.IsNullOrEmpty(opts.JsonFile)) SaveJSON(route, opts.JsonFile);
            }
        }
    }
}
