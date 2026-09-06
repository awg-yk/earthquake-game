using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthquakeGame
{
    // Loads Assets/Data/earthquakes.json and answers "did an earthquake
    // happen on this date, and what was observed in this prefecture?".
    // Keeps earthquake data completely separate from game logic so the
    // fake Phase 1 data can later be swapped for real JMA data without
    // touching this class's public API.
    public class EarthquakeManager : MonoBehaviour
    {
        [Tooltip("Resources-relative path, without extension, to the earthquake JSON file.")]
        public string dataResourcePath = "Data/earthquakes";

        // date string ("yyyy-MM-dd") -> all earthquakes that occurred that day
        private Dictionary<string, List<EarthquakeEvent>> eventsByDate = new Dictionary<string, List<EarthquakeEvent>>();

        public void LoadData()
        {
            eventsByDate.Clear();

            TextAsset json = Resources.Load<TextAsset>(dataResourcePath);
            if (json == null)
            {
                Debug.LogError($"EarthquakeManager: could not find Resources/{dataResourcePath}.json");
                return;
            }

            var root = MiniJson.Deserialize(json.text) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("earthquakes", out var listObj))
            {
                Debug.LogError("EarthquakeManager: earthquakes.json is missing an 'earthquakes' array");
                return;
            }

            foreach (var entryObj in (List<object>)listObj)
            {
                var entry = (Dictionary<string, object>)entryObj;
                var ev = new EarthquakeEvent
                {
                    date = (string)entry["date"],
                    time = entry.TryGetValue("time", out var t) ? (string)t : "",
                    epicenter = entry.TryGetValue("epicenter", out var e) ? (string)e : "",
                    magnitude = entry.TryGetValue("magnitude", out var m) ? Convert.ToDouble(m) : 0.0,
                    latitude = entry.TryGetValue("latitude", out var la) ? Convert.ToDouble(la) : 0.0,
                    longitude = entry.TryGetValue("longitude", out var lo) ? Convert.ToDouble(lo) : 0.0
                };

                if (entry.TryGetValue("intensities", out var intensitiesObj))
                {
                    var intensities = (Dictionary<string, object>)intensitiesObj;
                    foreach (var kv in intensities)
                    {
                        ev.intensities[kv.Key] = (string)kv.Value;
                    }
                }

                if (!eventsByDate.TryGetValue(ev.date, out var list))
                {
                    list = new List<EarthquakeEvent>();
                    eventsByDate[ev.date] = list;
                }
                list.Add(ev);
            }

            Debug.Log($"EarthquakeManager: loaded earthquakes for {eventsByDate.Count} distinct dates");
        }

        // Returns every earthquake recorded for the given date, or an empty list if none.
        public List<EarthquakeEvent> GetEarthquakesOn(DateTime date)
        {
            string key = date.ToString("yyyy-MM-dd");
            return eventsByDate.TryGetValue(key, out var list) ? list : new List<EarthquakeEvent>();
        }

        // Scans the whole given year and returns whichever prefecture felt
        // the strongest single shindo at any point that year - used to pick
        // where the player starts, so every playthrough opens at that
        // year's most dramatic location instead of a fixed prefecture.
        public string GetPrefectureWithStrongestQuake(int year)
        {
            string bestPrefecture = null;
            int bestRank = -1;

            int daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
            DateTime day = new DateTime(year, 1, 1);

            for (int i = 0; i < daysInYear; i++)
            {
                foreach (var ev in GetEarthquakesOn(day))
                {
                    foreach (var kv in ev.intensities)
                    {
                        int rank = IntensityScale.ToRank(kv.Value);
                        if (rank > bestRank)
                        {
                            bestRank = rank;
                            bestPrefecture = kv.Key;
                        }
                    }
                }
                day = day.AddDays(1);
            }

            return bestPrefecture;
        }

        // Scans every day of the given month for earthquakes that hit any
        // prefecture at or above minRank, anywhere in Japan (not just the
        // player's location). Used by the fortune teller's monthly
        // forecast: it peeks at the whole month's future data at once and
        // returns, per prefecture, the strongest intensity observed that
        // month (only for prefectures that reached minRank at least once).
        public Dictionary<string, string> GetPrefecturesWithQuakesInMonth(int year, int month, int minRank)
        {
            var result = new Dictionary<string, string>();
            int daysInMonth = DateTime.DaysInMonth(year, month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                var events = GetEarthquakesOn(new DateTime(year, month, day));
                foreach (var ev in events)
                {
                    foreach (var kv in ev.intensities)
                    {
                        int rank = IntensityScale.ToRank(kv.Value);
                        if (rank < minRank) continue;

                        if (!result.TryGetValue(kv.Key, out var existing) || rank > IntensityScale.ToRank(existing))
                        {
                            result[kv.Key] = kv.Value;
                        }
                    }
                }
            }

            return result;
        }
    }
}
