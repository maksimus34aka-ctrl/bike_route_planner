// bike_route_planner.go
package main

import (
	"encoding/json"
	"flag"
	"fmt"
	"math"
	"os"
	"strconv"
	"strings"
	"crypto/md5"
	"encoding/hex"
)

type Point struct {
	Lat  float64 `json:"lat"`
	Lng  float64 `json:"lng"`
	Name string  `json:"name"`
}

type Route struct {
	Start          Point    `json:"start"`
	End            Point    `json:"end"`
	Waypoints      []Point  `json:"waypoints"`
	TotalDistanceKm float64 `json:"total_distance_km"`
	ElevationGainM int     `json:"elevation_gain_m"`
	TimeHours      float64 `json:"time_hours"`
	Profile        string  `json:"profile"`
	Points         []Point `json:"-"`
}

type Planner struct {
	start     Point
	end       Point
	waypoints []Point
	profile   string
	color     bool
	route     *Route
}

func NewPlanner(start, end string, waypoints []string, profile string, color bool) *Planner {
	return &Planner{
		start:     parseLocation(start),
		end:       parseLocation(end),
		waypoints: parseWaypoints(waypoints),
		profile:   profile,
		color:     color,
	}
}

func parseLocation(loc string) Point {
	if strings.Contains(loc, ",") {
		parts := strings.Split(loc, ",")
		if lat, err1 := strconv.ParseFloat(strings.TrimSpace(parts[0]), 64); err1 == nil {
			if lng, err2 := strconv.ParseFloat(strings.TrimSpace(parts[1]), 64); err2 == nil {
				return Point{Lat: lat, Lng: lng, Name: loc}
			}
		}
	}
	return Point{Name: loc, Lat: 0, Lng: 0}
}

func parseWaypoints(ws []string) []Point {
	var pts []Point
	for _, w := range ws {
		pts = append(pts, parseLocation(w))
	}
	return pts
}

func geocode(name string) Point {
	hash := md5.Sum([]byte(name))
	h := int(hash[0])<<24 | int(hash[1])<<16 | int(hash[2])<<8 | int(hash[3])
	h = h % 360
	return Point{
		Lat:  50 + float64(h%100)/100,
		Lng:  10 + float64(h%200)/100,
		Name: name,
	}
}

func haversine(lat1, lon1, lat2, lon2 float64) float64 {
	const R = 6371.0
	phi1 := lat1 * math.Pi / 180
	phi2 := lat2 * math.Pi / 180
	dphi := (lat2 - lat1) * math.Pi / 180
	dlambda := (lon2 - lon1) * math.Pi / 180
	a := math.Sin(dphi/2)*math.Sin(dphi/2) + math.Cos(phi1)*math.Cos(phi2)*math.Sin(dlambda/2)*math.Sin(dlambda/2)
	return R * 2 * math.Atan2(math.Sqrt(a), math.Sqrt(1-a))
}

func getSpeed(profile string) float64 {
	switch profile {
	case "road": return 25
	case "mtb": return 15
	default: return 20
	}
}

func (p *Planner) CalculateRoute() {
	if p.start.Lat == 0 { p.start = geocode(p.start.Name) }
	if p.end.Lat == 0 { p.end = geocode(p.end.Name) }
	for i := range p.waypoints {
		if p.waypoints[i].Lat == 0 {
			p.waypoints[i] = geocode(p.waypoints[i].Name)
		}
	}
	points := []Point{p.start}
	points = append(points, p.waypoints...)
	points = append(points, p.end)

	var totalDist float64
	for i := 0; i < len(points)-1; i++ {
		totalDist += haversine(points[i].Lat, points[i].Lng, points[i+1].Lat, points[i+1].Lng)
	}
	speed := getSpeed(p.profile)
	timeHours := totalDist / speed
	elevGain := int(totalDist * 0.05 * 1000)

	p.route = &Route{
		Start:          p.start,
		End:            p.end,
		Waypoints:      p.waypoints,
		TotalDistanceKm: totalDist,
		ElevationGainM: elevGain,
		TimeHours:      timeHours,
		Profile:        p.profile,
		Points:         points,
	}
}

func (p *Planner) PrintRoute() {
	if p.route == nil { p.CalculateRoute() }
	r := p.route
	if p.color {
		fmt.Printf("\033[36m🚴 Маршрут от %s до %s\033[0m\n", p.start.Name, p.end.Name)
		if len(p.waypoints) > 0 {
			names := make([]string, len(p.waypoints))
			for i, w := range p.waypoints { names[i] = w.Name }
			fmt.Printf("\033[35m   через %s\033[0m\n", strings.Join(names, ", "))
		}
		fmt.Printf("\033[32mРасстояние: %.1f км\033[0m\n", r.TotalDistanceKm)
		fmt.Printf("\033[33mНабор высоты: %d м\033[0m\n", r.ElevationGainM)
		fmt.Printf("\033[34mВремя в пути: ~%.1f ч\033[0m\n", r.TimeHours)
	} else {
		fmt.Printf("Маршрут от %s до %s\n", p.start.Name, p.end.Name)
		if len(p.waypoints) > 0 {
			names := make([]string, len(p.waypoints))
			for i, w := range p.waypoints { names[i] = w.Name }
			fmt.Printf("   через %s\n", strings.Join(names, ", "))
		}
		fmt.Printf("Расстояние: %.1f км\n", r.TotalDistanceKm)
		fmt.Printf("Набор высоты: %d м\n", r.ElevationGainM)
		fmt.Printf("Время в пути: ~%.1f ч\n", r.TimeHours)
	}
}

func (p *Planner) ExportGPX(filename string) {
	if p.route == nil { p.CalculateRoute() }
	gpx := `<?xml version="1.0" encoding="UTF-8"?>
<gpx version="1.1" creator="BikeRoutePlanner">
  <trk>
    <trkseg>
`
	for _, pt := range p.route.Points {
		gpx += fmt.Sprintf(`      <trkpt lat="%.6f" lon="%.6f">
        <ele>%d</ele>
      </trkpt>
`, pt.Lat, pt.Lng, int(50+pt.Lat*10))
	}
	gpx += `    </trkseg>
  </trk>
</gpx>`
	os.WriteFile(filename, []byte(gpx), 0644)
	fmt.Printf("GPX экспортирован в %s\n", filename)
}

func (p *Planner) SaveJSON(filename string) {
	if p.route == nil { p.CalculateRoute() }
	data, _ := json.MarshalIndent(p.route, "", "  ")
	os.WriteFile(filename, data, 0644)
	fmt.Printf("Маршрут сохранён в %s\n", filename)
}

func main() {
	var (
		start     string
		end       string
		waypoints string
		profile   string
		gpx       string
		jsonOut   string
		color     bool
	)
	flag.StringVar(&start, "start", "", "Начальная точка")
	flag.StringVar(&end, "end", "", "Конечная точка")
	flag.StringVar(&waypoints, "waypoints", "", "Промежуточные точки через запятую")
	flag.StringVar(&profile, "profile", "touring", "road, mtb, touring")
	flag.StringVar(&gpx, "export-gpx", "", "Экспорт в GPX")
	flag.StringVar(&jsonOut, "save-json", "", "Сохранить в JSON")
	flag.BoolVar(&color, "color", false, "Принудительный цветной вывод")
	flag.Parse()

	if start == "" || end == "" {
		fmt.Fprintln(os.Stderr, "Error: --start and --end are required")
		os.Exit(1)
	}
	var ws []string
	if waypoints != "" {
		ws = strings.Split(waypoints, ",")
	}
	planner := NewPlanner(start, end, ws, profile, color || isTerminal())
	planner.CalculateRoute()
	planner.PrintRoute()
	if gpx != "" { planner.ExportGPX(gpx) }
	if jsonOut != "" { planner.SaveJSON(jsonOut) }
}

func isTerminal() bool {
	stat, _ := os.Stdout.Stat()
	return (stat.Mode() & os.ModeCharDevice) != 0
}
