#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Convert the JMA (Japan Meteorological Agency) 震度データベース bulk DAT file
into the Assets/Resources/Data/earthquakes.json format used by this game.

Usage:
    python tools/convert_jma_dat.py <shindo.dat> <code_p.dat> \
        --output Assets/Resources/Data/earthquakes.json \
        --prefectures Assets/Resources/Data/prefectures.json \
        --min-magnitude 6.5 --min-intensity-rank 5 \
        --start-year 2000 --end-year 2024

Both input files are fixed encoding Shift-JIS (cp932), as published by JMA.
The record layout below follows the JMA-provided format specification
(震度・加速度値ファイル), field positions given as 1-based inclusive ranges
in the spec, converted to 0-based Python slices here.

This script does NOT try to be a complete implementation of every historical
edge case in the format (e.g. clustered-earthquake "B"/"D" grouping records,
pre-1961 depth encodings). It focuses on producing usable per-prefecture
intensity data for notable earthquakes, which is what the game needs.
"""

import argparse
import json
import sys
from pathlib import Path

RECORD_LENGTH = 96

# JMA per-station intensity code -> the string format already used by
# earthquakes.json / IntensityScale.ToRank() in the Unity project.
STATION_INTENSITY_MAP = {
    "1": "1", "2": "2", "3": "3", "4": "4",
    "5": "5弱", "6": "6弱",  # pre-1996 undifferentiated 5/6, treated conservatively
    "7": "7",
    "A": "5弱", "B": "5強", "C": "6弱", "D": "6強",
}

# Header (震源レコード) max-intensity code -> same string format, used only
# as a fallback for very old records (pre-1961) that have no per-station
# detail lines at all.
HEADER_INTENSITY_MAP = {
    "1": "1", "2": "2", "3": "3", "4": "4",
    "5": "5弱", "6": "6弱", "7": "7",
    "A": "5弱", "B": "5強", "C": "6弱", "D": "6強",
}

INTENSITY_RANK = {
    "1": 1, "2": 2, "3": 3, "4": 4,
    "5弱": 5, "5強": 6, "6弱": 7, "6強": 8, "7": 9,
}


def rank_of(intensity_str):
    return INTENSITY_RANK.get(intensity_str, 0)


def decode(raw_bytes):
    return raw_bytes.decode("cp932", errors="ignore").strip()


def to_int(raw_bytes, default=None):
    s = decode(raw_bytes)
    if not s or not s.strip("-").isdigit():
        return default
    return int(s)


def load_prefecture_centroids(path):
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    centroids = []
    for entry in data["prefectures"]:
        centroids.append((entry["name"], float(entry.get("lat", 0)), float(entry.get("lon", 0))))
    return centroids


def nearest_prefecture(lat, lon, centroids):
    best_name, best_dist = None, None
    for name, plat, plon in centroids:
        d = (lat - plat) ** 2 + (lon - plon) ** 2
        if best_dist is None or d < best_dist:
            best_dist, best_name = d, name
    return best_name


def load_station_coordinates(code_p_path):
    """code_p.dat: tab-separated text (Shift-JIS).
    Columns: station_number, name, lat(度分, e.g. "3536"), lon(度分, e.g. "13340"), start, end
    """
    stations = {}
    with open(code_p_path, encoding="cp932", errors="ignore") as f:
        for line in f:
            cols = line.rstrip("\r\n").split("\t")
            if len(cols) < 4:
                continue
            try:
                station_id = int(cols[0].strip())
            except ValueError:
                continue

            lat_raw = cols[2].strip()
            lon_raw = cols[3].strip()
            lat = _parse_deg_min(lat_raw)
            lon = _parse_deg_min(lon_raw)
            if lat is None or lon is None:
                continue
            stations[station_id] = (lat, lon)
    return stations


def _parse_deg_min(raw):
    """Parse a "度分" digit string (e.g. "3536" -> 35 deg 36 min) into decimal degrees."""
    if not raw or not raw.isdigit():
        return None
    minute = int(raw[-2:])
    degree = int(raw[:-2]) if len(raw) > 2 else 0
    return degree + minute / 60.0


def extract_epicenter_name(raw_bytes):
    name = decode(raw_bytes)
    return name.replace("　", " ").strip()


def guess_prefecture_from_epicenter_name(name, centroids):
    for pref_name, _, _ in centroids:
        # Prefecture names end in 都/道/府/県; a lot of epicenter names embed
        # the prefecture name directly (e.g. "徳島県南部", "広島県北部").
        if pref_name in name:
            return pref_name
    return None


def parse_header_record(line):
    year = to_int(line[1:5])
    month = to_int(line[5:7])
    day = to_int(line[7:9])
    hour = to_int(line[9:11], 0)
    minute = to_int(line[11:13], 0)

    lat_deg = to_int(line[21:24], 0)
    lat_min_raw = decode(line[24:28])
    lon_deg = to_int(line[32:36], 0)
    lon_min_raw = decode(line[36:40])

    try:
        lat_min = float(lat_min_raw) / 100.0 if lat_min_raw.strip() else 0.0
    except ValueError:
        lat_min = 0.0
    try:
        lon_min = float(lon_min_raw) / 100.0 if lon_min_raw.strip() else 0.0
    except ValueError:
        lon_min = 0.0

    latitude = (lat_deg or 0) + lat_min / 60.0
    longitude = (lon_deg or 0) + lon_min / 60.0

    mag_raw = decode(line[52:54])
    magnitude = None
    if mag_raw.strip() and mag_raw.strip("-").replace(" ", "").isdigit():
        magnitude = int(mag_raw) / 10.0

    max_intensity_code = decode(line[61:62])
    max_intensity = HEADER_INTENSITY_MAP.get(max_intensity_code)

    epicenter_name = extract_epicenter_name(line[68:90])

    if not year or not month or not day:
        return None

    return {
        "year": year, "month": month, "day": day, "hour": hour, "minute": minute,
        "latitude": latitude, "longitude": longitude,
        "magnitude": magnitude,
        "max_intensity": max_intensity,
        "epicenter": epicenter_name,
    }


def parse_station_record(line):
    station_id = to_int(line[0:7])
    intensity_code = decode(line[18:19])
    intensity = STATION_INTENSITY_MAP.get(intensity_code)
    if station_id is None or intensity is None:
        return None
    return station_id, intensity


def convert(dat_path, code_p_path, prefectures_path, min_magnitude, min_intensity_rank, start_year, end_year):
    centroids = load_prefecture_centroids(prefectures_path)
    stations = load_station_coordinates(code_p_path)

    events = []
    current_header = None
    current_intensities = {}  # prefecture -> best intensity string seen so far

    def finalize():
        if current_header is None:
            return
        h = current_header
        if not current_intensities:
            # Very old records (pre-1961) have no per-station detail lines.
            if h["max_intensity"] is not None:
                pref = guess_prefecture_from_epicenter_name(h["epicenter"], centroids)
                if pref:
                    current_intensities[pref] = h["max_intensity"]

        if not current_intensities:
            return

        magnitude = h["magnitude"] or 0.0
        max_rank = max((rank_of(v) for v in current_intensities.values()), default=0)

        if magnitude < min_magnitude and max_rank < min_intensity_rank:
            return
        if start_year and h["year"] < start_year:
            return
        if end_year and h["year"] > end_year:
            return

        events.append({
            "date": f"{h['year']:04d}-{h['month']:02d}-{h['day']:02d}",
            "time": f"{h['hour']:02d}:{h['minute']:02d}",
            "epicenter": h["epicenter"],
            "latitude": round(h["latitude"], 2),
            "longitude": round(h["longitude"], 2),
            "magnitude": magnitude,
            "intensities": dict(current_intensities),
        })

    with open(dat_path, "rb") as f:
        for raw_line in f:
            line = raw_line.rstrip(b"\r\n")
            if len(line) < RECORD_LENGTH:
                line = line.ljust(RECORD_LENGTH, b" ")
            record_type = chr(line[0]) if line[0] < 128 else "?"

            if record_type in ("A", "B", "D"):
                finalize()
                current_header = parse_header_record(line)
                current_intensities = {}
            else:
                if current_header is None:
                    continue
                parsed = parse_station_record(line)
                if parsed is None:
                    continue
                station_id, intensity = parsed
                coords = stations.get(station_id)
                if coords is None:
                    continue
                pref = nearest_prefecture(coords[0], coords[1], centroids)
                if pref is None:
                    continue
                existing = current_intensities.get(pref)
                if existing is None or rank_of(intensity) > rank_of(existing):
                    current_intensities[pref] = intensity

        finalize()

    events.sort(key=lambda e: (e["date"], e["time"]))
    return events


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("dat_file", help="Path to the JMA shindo DAT file (Shift-JIS, fixed-length)")
    parser.add_argument("code_p_file", help="Path to code_p.dat (station master file)")
    parser.add_argument("--prefectures", default="Assets/Resources/Data/prefectures.json")
    parser.add_argument("--output", default="Assets/Resources/Data/earthquakes.json")
    parser.add_argument("--min-magnitude", type=float, default=6.5)
    parser.add_argument("--min-intensity-rank", type=int, default=5, help="5=震度5弱, 6=5強, 7=6弱, 8=6強, 9=7")
    parser.add_argument("--start-year", type=int, default=None)
    parser.add_argument("--end-year", type=int, default=None)
    args = parser.parse_args()

    events = convert(
        args.dat_file, args.code_p_file, args.prefectures,
        args.min_magnitude, args.min_intensity_rank,
        args.start_year, args.end_year,
    )

    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump({"earthquakes": events}, f, ensure_ascii=False, indent=2)

    print(f"Wrote {len(events)} earthquakes to {output_path}", file=sys.stderr)


if __name__ == "__main__":
    main()
