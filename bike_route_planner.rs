// bike_route_planner.rs
use clap::{App, Arg};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::fs;
use std::f64::consts::PI;
use colored::*;

#[derive(Debug, Clone, Serialize, Deserialize)]
struct Point {
    lat: f64,
    lng: f64,
    name: String,
}

#[derive(Debug, Serialize)]
struct Route {
    start: Point,
    end: Point,
    waypoints: Vec<Point>,
    total_distance_km: f64,
    elevation_gain_m: i32,
    time_hours: f64,
    profile: String,
    points: Vec<Point>,
}

struct Planner {
    start: Point,
    end: Point,
    waypoints: Vec<Point>,
    profile: String,
    color: bool,
    route: Option<Route>,
}

impl Planner {
    fn new(start: &str, end: &str, waypoints: Vec<&str>, profile: &str, color: bool) -> Self {
        Planner {
            start: Self::parse_location(start),
            end: Self::parse_location(end),
            waypoints: waypoints.into_iter().map(Self::parse_location).collect(),
            profile: profile.to_string(),
            color,
            route: None,
        }
    }

    fn parse_location(loc: &str) -> Point {
        if let Some((lat_str, lng_str)) = loc.split_once(',') {
            if let (Ok(lat), Ok(lng)) = (lat_str.trim().parse(), lng_str.trim().parse()) {
                return Point { lat, lng, name: loc.to_string() };
            }
        }
        Point { lat: 0.0, lng: 0.0, name: loc.to_string() }
    }

    fn geocode(name: &str) -> Point {
        use std::collections::hash_map::DefaultHasher;
        use std::hash::{Hash, Hasher};
        let mut hasher = DefaultHasher::new();
        name.hash(&mut hasher);
        let h = hasher.finish() % 360;
        Point {
            lat: 50.0 + (h % 100) as f64 / 100.0,
            lng: 10.0 + (h % 200) as f64 / 100.0,
            name: name.to_string(),
        }
    }

    fn haversine(lat1: f64, lon1: f64, lat2: f64, lon2: f64) -> f64 {
        const R: f64 = 6371.0;
        let phi1 = lat1 * PI / 180.0;
        let phi2 = lat2 * PI / 180.0;
        let dphi = (lat2 - lat1) * PI / 180.0;
        let dlambda = (lon2 - lon1) * PI / 180.0;
        let a = (dphi/2.0).sin().powi(2) + phi1.cos() * phi2.cos() * (dlambda/2.0).sin().powi(2);
        R * 2.0 * a.sqrt().atan2((1.0 - a).sqrt())
    }

    fn get_speed(profile: &str) -> f64 {
        match profile {
            "road" => 25.0,
            "mtb" => 15.0,
            _ => 20.0,
        }
    }

    fn calculate_route(&mut self) {
        if self.start.lat == 0.0 { self.start = Self::geocode(&self.start.name); }
        if self.end.lat == 0.0 { self.end = Self::geocode(&self.end.name); }
        for w in &mut self.waypoints {
            if w.lat == 0.0 { *w = Self::geocode(&w.name); }
        }
        let mut points = vec![self.start.clone()];
        points.extend(self.waypoints.clone());
        points.push(self.end.clone());

        let mut total_dist = 0.0;
        for i in 0..points.len()-1 {
            total_dist += Self::haversine(points[i].lat, points[i].lng, points[i+1].lat, points[i+1].lng);
        }
        let speed = Self::get_speed(&self.profile);
        let time_hours = total_dist / speed;
        let elev_gain = (total_dist * 0.05 * 1000.0) as i32;

        self.route = Some(Route {
            start: self.start.clone(),
            end: self.end.clone(),
            waypoints: self.waypoints.clone(),
            total_distance_km: total_dist,
            elevation_gain_m: elev_gain,
            time_hours,
            profile: self.profile.clone(),
            points,
        });
    }

    fn print_route(&self) {
        let r = self.route.as_ref().unwrap();
        if self.color {
            println!("{}", format!("🚴 Маршрут от {} до {}", self.start.name, self.end.name).cyan());
            if !self.waypoints.is_empty() {
                let names: Vec<_> = self.waypoints.iter().map(|w| w.name.as_str()).collect();
                println!("{}", format!("   через {}", names.join(", ")).magenta());
            }
            println!("{}", format!("Расстояние: {:.1} км", r.total_distance_km).green());
            println!("{}", format!("Набор высоты: {} м", r.elevation_gain_m).yellow());
            println!("{}", format!("Время в пути: ~{:.1} ч", r.time_hours).blue());
        } else {
            println!("Маршрут от {} до {}", self.start.name, self.end.name);
            if !self.waypoints.is_empty() {
                let names: Vec<_> = self.waypoints.iter().map(|w| w.name.as_str()).collect();
                println!("   через {}", names.join(", "));
            }
            println!("Расстояние: {:.1} км", r.total_distance_km);
            println!("Набор высоты: {} м", r.elevation_gain_m);
            println!("Время в пути: ~{:.1} ч", r.time_hours);
        }
    }

    fn export_gpx(&self, filename: &str) {
        let r = self.route.as_ref().unwrap();
        let mut gpx = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n".to_string();
        gpx.push_str("<gpx version=\"1.1\" creator=\"BikeRoutePlanner\">\n");
        gpx.push_str("  <trk>\n    <trkseg>\n");
        for pt in &r.points {
            gpx.push_str(&format!("      <trkpt lat=\"{:.6}\" lon=\"{:.6}\">\n", pt.lat, pt.lng));
            gpx.push_str(&format!("        <ele>{}</ele>\n", (50.0 + pt.lat * 10.0) as i32));
            gpx.push_str("      </trkpt>\n");
        }
        gpx.push_str("    </trkseg>\n  </trk>\n</gpx>");
        fs::write(filename, gpx).unwrap();
        println!("GPX экспортирован в {}", filename);
    }

    fn save_json(&self, filename: &str) {
        let json = serde_json::to_string_pretty(&self.route.as_ref().unwrap()).unwrap();
        fs::write(filename, json).unwrap();
        println!("Маршрут сохранён в {}", filename);
    }
}

fn main() {
    let matches = App::new("Bike Route Planner")
        .arg(Arg::with_name("start").long("start").takes_value(true).required(true))
        .arg(Arg::with_name("end").long("end").takes_value(true).required(true))
        .arg(Arg::with_name("waypoints").long("waypoints").takes_value(true))
        .arg(Arg::with_name("profile").long("profile").takes_value(true).default_value("touring"))
        .arg(Arg::with_name("export-gpx").long("export-gpx").takes_value(true))
        .arg(Arg::with_name("save-json").long("save-json").takes_value(true))
        .arg(Arg::with_name("color").long("color"))
        .get_matches();

    let start = matches.value_of("start").unwrap();
    let end = matches.value_of("end").unwrap();
    let waypoints = matches.value_of("waypoints").map(|s| s.split(',').collect()).unwrap_or(vec![]);
    let profile = matches.value_of("profile").unwrap();
    let color = matches.is_present("color") || atty::is(atty::Stream::Stdout);

    let mut planner = Planner::new(start, end, waypoints, profile, color);
    planner.calculate_route();
    planner.print_route();
    if let Some(gpx) = matches.value_of("export-gpx") { planner.export_gpx(gpx); }
    if let Some(json) = matches.value_of("save-json") { planner.save_json(json); }
}
