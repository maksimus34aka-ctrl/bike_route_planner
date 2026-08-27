// bike_route_planner.js
const { program } = require('commander');
const fs = require('fs');
const chalk = require('chalk');
const crypto = require('crypto');

class RoutePlanner {
    constructor(start, end, waypoints, profile, color) {
        this.start = this.parseLocation(start);
        this.end = this.parseLocation(end);
        this.waypoints = waypoints ? waypoints.split(',').map(w => this.parseLocation(w.trim())) : [];
        this.profile = profile || 'touring';
        this.color = color && process.stdout.isTTY;
        this.route = null;
    }

    parseLocation(loc) {
        if (loc.includes(',') && !isNaN(parseFloat(loc.split(',')[0])) && !isNaN(parseFloat(loc.split(',')[1]))) {
            const [lat, lng] = loc.split(',').map(Number);
            return { lat, lng, name: loc };
        }
        return { name: loc, lat: 0, lng: 0 };
    }

    geocode(name) {
        // Фиктивное геокодирование для демонстрации
        const hash = crypto.createHash('md5').update(name).digest('hex');
        const h = parseInt(hash.substring(0, 8), 16) % 360;
        return { lat: 50 + (h % 100) / 100, lng: 10 + (h % 200) / 100 };
    }

    haversine(lat1, lon1, lat2, lon2) {
        const R = 6371;
        const phi1 = lat1 * Math.PI / 180;
        const phi2 = lat2 * Math.PI / 180;
        const dphi = (lat2 - lat1) * Math.PI / 180;
        const dlambda = (lon2 - lon1) * Math.PI / 180;
        const a = Math.sin(dphi/2)**2 + Math.cos(phi1)*Math.cos(phi2)*Math.sin(dlambda/2)**2;
        return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1-a));
    }

    getSpeed() {
        const speeds = { road: 25, mtb: 15, touring: 20 };
        return speeds[this.profile] || 20;
    }

    calculateRoute() {
        if (this.start.lat === 0) Object.assign(this.start, this.geocode(this.start.name));
        if (this.end.lat === 0) Object.assign(this.end, this.geocode(this.end.name));
        for (let w of this.waypoints) {
            if (w.lat === 0) Object.assign(w, this.geocode(w.name));
        }
        const points = [this.start, ...this.waypoints, this.end];
        let totalDistance = 0;
        for (let i = 0; i < points.length - 1; i++) {
            totalDistance += this.haversine(points[i].lat, points[i].lng, points[i+1].lat, points[i+1].lng);
        }
        const speed = this.getSpeed();
        const timeHours = totalDistance / speed;
        const elevationGain = Math.round(totalDistance * 0.05 * 1000);
        this.route = {
            start: this.start,
            end: this.end,
            waypoints: this.waypoints,
            totalDistanceKm: parseFloat(totalDistance.toFixed(2)),
            elevationGainM: elevationGain,
            timeHours: parseFloat(timeHours.toFixed(2)),
            profile: this.profile,
            points
        };
        return this.route;
    }

    printRoute() {
        if (!this.route) this.calculateRoute();
        const r = this.route;
        if (this.color) {
            console.log(chalk.cyan(`🚴 Маршрут от ${this.start.name} до ${this.end.name}`));
            if (this.waypoints.length) {
                console.log(chalk.magenta(`   через ${this.waypoints.map(w => w.name).join(', ')}`));
            }
            console.log(chalk.green(`Расстояние: ${r.totalDistanceKm.toFixed(1)} км`));
            console.log(chalk.yellow(`Набор высоты: ${r.elevationGainM} м`));
            console.log(chalk.blue(`Время в пути: ~${r.timeHours.toFixed(1)} ч`));
        } else {
            console.log(`Маршрут от ${this.start.name} до ${this.end.name}`);
            if (this.waypoints.length) {
                console.log(`   через ${this.waypoints.map(w => w.name).join(', ')}`);
            }
            console.log(`Расстояние: ${r.totalDistanceKm.toFixed(1)} км`);
            console.log(`Набор высоты: ${r.elevationGainM} м`);
            console.log(`Время в пути: ~${r.timeHours.toFixed(1)} ч`);
        }
    }

    exportGPX(filename) {
        if (!this.route) this.calculateRoute();
        let gpx = '<?xml version="1.0" encoding="UTF-8"?>\n';
        gpx += '<gpx version="1.1" creator="BikeRoutePlanner">\n';
        gpx += '  <trk>\n    <trkseg>\n';
        for (const pt of this.route.points) {
            gpx += `      <trkpt lat="${pt.lat.toFixed(6)}" lon="${pt.lng.toFixed(6)}">\n`;
            gpx += `        <ele>${Math.round(50 + pt.lat * 10)}</ele>\n`;
            gpx += '      </trkpt>\n';
        }
        gpx += '    </trkseg>\n  </trk>\n</gpx>';
        fs.writeFileSync(filename, gpx);
        console.log(`GPX экспортирован в ${filename}`);
    }

    saveJSON(filename) {
        if (!this.route) this.calculateRoute();
        fs.writeFileSync(filename, JSON.stringify(this.route, null, 2));
        console.log(`Маршрут сохранён в ${filename}`);
    }
}

program
    .requiredOption('--start <point>', 'Начальная точка')
    .requiredOption('--end <point>', 'Конечная точка')
    .option('--waypoints <list>', 'Промежуточные точки через запятую')
    .option('--profile <type>', 'road, mtb, touring', 'touring')
    .option('--export-gpx <file>', 'Экспорт в GPX')
    .option('--save-json <file>', 'Сохранить маршрут в JSON')
    .option('--color', 'Принудительный цветной вывод')
    .parse(process.argv);

const opts = program.opts();
const planner = new RoutePlanner(opts.start, opts.end, opts.waypoints, opts.profile, opts.color);
planner.calculateRoute();
planner.printRoute();
if (opts.exportGpx) planner.exportGPX(opts.exportGpx);
if (opts.saveJson) planner.saveJSON(opts.saveJson);
