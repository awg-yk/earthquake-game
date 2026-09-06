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

        // Looks ahead from (exclusive) startDate for up to dayRange days and
        // returns the first earthquake found. Used by the fortune teller
        // (Phase 2) to peek at future data without the player seeing it directly.
        public EarthquakeEvent PeekFutureEarthquake(DateTime startDate, int dayRange)
        {
            for (int i = 1; i <= dayRange; i++)
            {
                var events = GetEarthquakesOn(startDate.AddDays(i));
                if (events.Count > 0) return events[0];
            }
            return null;
        }
    }
}
