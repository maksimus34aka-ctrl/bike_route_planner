// bike_route_planner.cpp
#include <iostream>
#include <string>
#include <vector>
#include <cmath>
#include <fstream>
#include <sstream>
#include <iomanip>
#include <cstdlib>
#include <json/json.h> // using jsoncpp

using namespace std;

struct Point {
    double lat, lng;
    string name;
};

struct Route {
    Point start, end;
    vector<Point> waypoints;
    double totalDistanceKm;
    int elevationGainM;
    double timeHours;
    string profile;
    vector<Point> points;
};

Point parseLocation(const string& loc) {
    size_t comma = loc.find(',');
    if (comma != string::npos) {
        string latStr = loc.substr(0, comma);
        string lngStr = loc.substr(comma+1);
        try {
            double lat = stod(latStr);
            double lng = stod(lngStr);
            return {lat, lng, loc};
        } catch (...) {}
    }
    return {0, 0, loc};
}

Point geocode(const string& name) {
    int hash = 0;
    for (char c : name) hash = (hash * 31 + c) % 360;
    if (hash < 0) hash += 360;
    return {50.0 + (hash % 100) / 100.0, 10.0 + (hash % 200) / 100.0, name};
}

double haversine(double lat1, double lon1, double lat2, double lon2) {
    const double R = 6371.0;
    double phi1 = lat1 * M_PI / 180.0;
    double phi2 = lat2 * M_PI / 180.0;
    double dphi = (lat2 - lat1) * M_PI / 180.0;
    double dlambda = (lon2 - lon1) * M_PI / 180.0;
    double a = sin(dphi/2)*sin(dphi/2) + cos(phi1)*cos(phi2)*sin(dlambda/2)*sin(dlambda/2);
    return R * 2 * atan2(sqrt(a), sqrt(1-a));
}

double getSpeed(const string& profile) {
    if (profile == "road") return 25.0;
    if (profile == "mtb") return 15.0;
    return 20.0;
}

Route calculateRoute(const string& startStr, const string& endStr, const vector<string>& wayStrs, const string& profile) {
    Point start = parseLocation(startStr);
    Point end = parseLocation(endStr);
    vector<Point> waypoints;
    for (const auto& w : wayStrs) waypoints.push_back(parseLocation(w));
    if (start.lat == 0) start = geocode(start.name);
    if (end.lat == 0) end = geocode(end.name);
    for (auto& w : waypoints) {
        if (w.lat == 0) w = geocode(w.name);
    }
    vector<Point> points;
    points.push_back(start);
    points.insert(points.end(), waypoints.begin(), waypoints.end());
    points.push_back(end);
    double totalDist = 0;
    for (size_t i = 0; i < points.size()-1; ++i) {
        totalDist += haversine(points[i].lat, points[i].lng, points[i+1].lat, points[i+1].lng);
    }
    double speed = getSpeed(profile);
    double timeHours = totalDist / speed;
    int elevGain = (int)(totalDist * 0.05 * 1000);
    Route r;
    r.start = start; r.end = end; r.waypoints = waypoints;
    r.totalDistanceKm = totalDist; r.elevationGainM = elevGain;
    r.timeHours = timeHours; r.profile = profile; r.points = points;
    return r;
}

void printRoute(const Route& r, bool color) {
    if (color) {
        cout << "\033[36m🚴 Маршрут от " << r.start.name << " до " << r.end.name << "\033[0m" << endl;
        if (!r.waypoints.empty()) {
            cout << "\033[35m   через ";
            for (size_t i = 0; i < r.waypoints.size(); ++i) {
                if (i) cout << ", ";
                cout << r.waypoints[i].name;
            }
            cout << "\033[0m" << endl;
        }
        cout << "\033[32mРасстояние: " << fixed << setprecision(1) << r.totalDistanceKm << " км\033[0m" << endl;
        cout << "\033[33mНабор высоты: " << r.elevationGainM << " м\033[0m" << endl;
        cout << "\033[34mВремя в пути: ~" << fixed << setprecision(1) << r.timeHours << " ч\033[0m" << endl;
    } else {
        cout << "Маршрут от " << r.start.name << " до " << r.end.name << endl;
        if (!r.waypoints.empty()) {
            cout << "   через ";
            for (size_t i = 0; i < r.waypoints.size(); ++i) {
                if (i) cout << ", ";
                cout << r.waypoints[i].name;
            }
            cout << endl;
        }
        cout << "Расстояние: " << fixed << setprecision(1) << r.totalDistanceKm << " км" << endl;
        cout << "Набор высоты: " << r.elevationGainM << " м" << endl;
        cout << "Время в пути: ~" << fixed << setprecision(1) << r.timeHours << " ч" << endl;
    }
}

void exportGPX(const Route& r, const string& filename) {
    ofstream ofs(filename);
    ofs << "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
    ofs << "<gpx version=\"1.1\" creator=\"BikeRoutePlanner\">\n";
    ofs << "  <trk>\n    <trkseg>\n";
    for (const auto& pt : r.points) {
        ofs << "      <trkpt lat=\"" << fixed << setprecision(6) << pt.lat << "\" lon=\"" << pt.lng << "\">\n";
        ofs << "        <ele>" << (int)(50 + pt.lat * 10) << "</ele>\n";
        ofs << "      </trkpt>\n";
    }
    ofs << "    </trkseg>\n  </trk>\n</gpx>";
    cout << "GPX экспортирован в " << filename << endl;
}

void saveJSON(const Route& r, const string& filename) {
    Json::Value root;
    root["start"]["lat"] = r.start.lat; root["start"]["lng"] = r.start.lng; root["start"]["name"] = r.start.name;
    root["end"]["lat"] = r.end.lat; root["end"]["lng"] = r.end.lng; root["end"]["name"] = r.end.name;
    for (const auto& w : r.waypoints) {
        Json::Value jw;
        jw["lat"] = w.lat; jw["lng"] = w.lng; jw["name"] = w.name;
        root["waypoints"].append(jw);
    }
    root["total_distance_km"] = r.totalDistanceKm;
    root["elevation_gain_m"] = r.elevationGainM;
    root["time_hours"] = r.timeHours;
    root["profile"] = r.profile;
    ofstream ofs(filename);
    ofs << root.toStyledString();
    cout << "Маршрут сохранён в " << filename << endl;
}

int main(int argc, char* argv[]) {
    string start, end, waypointsStr, profile = "touring", gpxFile, jsonFile;
    bool color = false;

    for (int i = 1; i < argc; ++i) {
        string arg = argv[i];
        if (arg == "--start" && i+1 < argc) start = argv[++i];
        else if (arg == "--end" && i+1 < argc) end = argv[++i];
        else if (arg == "--waypoints" && i+1 < argc) waypointsStr = argv[++i];
        else if (arg == "--profile" && i+1 < argc) profile = argv[++i];
        else if (arg == "--export-gpx" && i+1 < argc) gpxFile = argv[++i];
        else if (arg == "--save-json" && i+1 < argc) jsonFile = argv[++i];
        else if (arg == "--color") color = true;
    }
    if (start.empty() || end.empty()) {
        cerr << "Error: --start and --end are required" << endl;
        return 1;
    }
    vector<string> waypoints;
    if (!waypointsStr.empty()) {
        stringstream ss(waypointsStr);
        string token;
        while (getline(ss, token, ',')) waypoints.push_back(token);
    }
    color = color || isatty(fileno(stdout));
    Route route = calculateRoute(start, end, waypoints, profile);
    printRoute(route, color);
    if (!gpxFile.empty()) exportGPX(route, gpxFile);
    if (!jsonFile.empty()) saveJSON(route, jsonFile);
    return 0;
}
