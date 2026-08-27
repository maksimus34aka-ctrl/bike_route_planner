// BikeRoutePlanner.java
import com.beust.jcommander.JCommander;
import com.beust.jcommander.Parameter;
import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import okhttp3.*;

import java.io.*;
import java.nio.file.*;
import java.util.*;

public class BikeRoutePlanner {
    @Parameter(names = "--start", required = true)
    private String start;
    @Parameter(names = "--end", required = true)
    private String end;
    @Parameter(names = "--waypoints")
    private String waypoints;
    @Parameter(names = "--profile")
    private String profile = "touring";
    @Parameter(names = "--export-gpx")
    private String gpxFile;
    @Parameter(names = "--save-json")
    private String jsonFile;
    @Parameter(names = "--color")
    private boolean color;

    static class Point {
        double lat, lng;
        String name;
        Point(double lat, double lng, String name) { this.lat=lat; this.lng=lng; this.name=name; }
    }

    static class Route {
        Point start, end;
        List<Point> waypoints;
        double totalDistanceKm;
        int elevationGainM;
        double timeHours;
        String profile;
        List<Point> points;
    }

    private Point parseLocation(String loc) {
        if (loc.contains(",")) {
            String[] parts = loc.split(",");
            try {
                double lat = Double.parseDouble(parts[0].trim());
                double lng = Double.parseDouble(parts[1].trim());
                return new Point(lat, lng, loc);
            } catch (NumberFormatException ignored) {}
        }
        return new Point(0, 0, loc);
    }

    private Point geocode(String name) {
        int hash = name.hashCode() % 360;
        if (hash < 0) hash += 360;
        return new Point(50 + (hash % 100) / 100.0, 10 + (hash % 200) / 100.0, name);
    }

    private double haversine(double lat1, double lon1, double lat2, double lon2) {
        final double R = 6371.0;
        double phi1 = Math.toRadians(lat1);
        double phi2 = Math.toRadians(lat2);
        double dphi = Math.toRadians(lat2 - lat1);
        double dlambda = Math.toRadians(lon2 - lon1);
        double a = Math.sin(dphi/2)*Math.sin(dphi/2) + Math.cos(phi1)*Math.cos(phi2)*Math.sin(dlambda/2)*Math.sin(dlambda/2);
        return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1-a));
    }

    private double getSpeed() {
        switch (profile) {
            case "road": return 25;
            case "mtb": return 15;
            default: return 20;
        }
    }

    private Route calculateRoute() {
        Point startP = parseLocation(start);
        Point endP = parseLocation(end);
        List<Point> wayPts = new ArrayList<>();
        if (waypoints != null) {
            for (String w : waypoints.split(",")) wayPts.add(parseLocation(w.trim()));
        }
        if (startP.lat == 0) startP = geocode(startP.name);
        if (endP.lat == 0) endP = geocode(endP.name);
        for (Point w : wayPts) {
            if (w.lat == 0) { Point g = geocode(w.name); w.lat=g.lat; w.lng=g.lng; }
        }
        List<Point> points = new ArrayList<>();
        points.add(startP);
        points.addAll(wayPts);
        points.add(endP);
        double totalDist = 0;
        for (int i = 0; i < points.size()-1; i++) {
            totalDist += haversine(points.get(i).lat, points.get(i).lng, points.get(i+1).lat, points.get(i+1).lng);
        }
        double speed = getSpeed();
        double timeHours = totalDist / speed;
        int elevGain = (int)(totalDist * 0.05 * 1000);
        Route route = new Route();
        route.start = startP;
        route.end = endP;
        route.waypoints = wayPts;
        route.totalDistanceKm = totalDist;
        route.elevationGainM = elevGain;
        route.timeHours = timeHours;
        route.profile = profile;
        route.points = points;
        return route;
    }

    private void printRoute(Route r) {
        if (color || System.console() != null) {
            System.out.println("\u001B[36m🚴 Маршрут от " + r.start.name + " до " + r.end.name + "\u001B[0m");
            if (!r.waypoints.isEmpty()) {
                String names = String.join(", ", r.waypoints.stream().map(w -> w.name).toArray(String[]::new));
                System.out.println("\u001B[35m   через " + names + "\u001B[0m");
            }
            System.out.printf("\u001B[32mРасстояние: %.1f км\u001B[0m\n", r.totalDistanceKm);
            System.out.printf("\u001B[33mНабор высоты: %d м\u001B[0m\n", r.elevationGainM);
            System.out.printf("\u001B[34mВремя в пути: ~%.1f ч\u001B[0m\n", r.timeHours);
        } else {
            System.out.println("Маршрут от " + r.start.name + " до " + r.end.name);
            if (!r.waypoints.isEmpty()) {
                String names = String.join(", ", r.waypoints.stream().map(w -> w.name).toArray(String[]::new));
                System.out.println("   через " + names);
            }
            System.out.printf("Расстояние: %.1f км\n", r.totalDistanceKm);
            System.out.printf("Набор высоты: %d м\n", r.elevationGainM);
            System.out.printf("Время в пути: ~%.1f ч\n", r.timeHours);
        }
    }

    private void exportGPX(Route r, String filename) throws IOException {
        StringBuilder gpx = new StringBuilder();
        gpx.append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        gpx.append("<gpx version=\"1.1\" creator=\"BikeRoutePlanner\">\n");
        gpx.append("  <trk>\n    <trkseg>\n");
        for (Point pt : r.points) {
            gpx.append(String.format("      <trkpt lat=\"%.6f\" lon=\"%.6f\">\n", pt.lat, pt.lng));
            gpx.append(String.format("        <ele>%d</ele>\n", (int)(50 + pt.lat * 10)));
            gpx.append("      </trkpt>\n");
        }
        gpx.append("    </trkseg>\n  </trk>\n</gpx>");
        Files.write(Paths.get(filename), gpx.toString().getBytes());
        System.out.println("GPX экспортирован в " + filename);
    }

    private void saveJSON(Route r, String filename) throws IOException {
        Gson gson = new GsonBuilder().setPrettyPrinting().create();
        Files.write(Paths.get(filename), gson.toJson(r).getBytes());
        System.out.println("Маршрут сохранён в " + filename);
    }

    public static void main(String[] args) throws Exception {
        BikeRoutePlanner planner = new BikeRoutePlanner();
        JCommander.newBuilder().addObject(planner).build().parse(args);
        Route route = planner.calculateRoute();
        planner.printRoute(route);
        if (planner.gpxFile != null) planner.exportGPX(route, planner.gpxFile);
        if (planner.jsonFile != null) planner.saveJSON(route, planner.jsonFile);
    }
}
