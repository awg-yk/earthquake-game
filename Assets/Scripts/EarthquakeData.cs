using System.Collections.Generic;

namespace EarthquakeGame
{
    // A single earthquake event as parsed from earthquakes.json.
    // Intensities are keyed by prefecture name (e.g. "宮城県" -> "6強").
    public class EarthquakeEvent
    {
        public string date; // "yyyy-MM-dd"
        public string time;
        public string epicenter;
        public double magnitude;
        public double latitude;
        public double longitude;
        public Dictionary<string, string> intensities = new Dictionary<string, string>();

        public string GetIntensityFor(string prefectureName)
        {
            return intensities.TryGetValue(prefectureName, out var v) ? v : null;
        }
    }

    // Converts the JMA-style intensity strings ("5弱", "6強", "7", ...)
    // into a comparable numeric scale so game rules can threshold on them.
    public static class IntensityScale
    {
        // Returns a value where higher = stronger shaking.
        // 1,2,3,4 -> 1..4 ; 5弱 -> 5, 5強 -> 6 ; 6弱 -> 7, 6強 -> 8 ; 7 -> 9
        public static int ToRank(string intensity)
        {
            if (string.IsNullOrEmpty(intensity)) return 0;
            switch (intensity)
            {
                case "1": return 1;
                case "2": return 2;
                case "3": return 3;
                case "4": return 4;
                case "5弱": return 5;
                case "5強": return 6;
                case "6弱": return 7;
                case "6強": return 8;
                case "7": return 9;
                default: return 0;
            }
        }

        public static bool IsAtLeast5Weak(string intensity)
        {
            return ToRank(intensity) >= 5;
        }

        // Hex color (no '#') used to render this intensity in the
        // earthquake intensity map/legend, roughly following the color
        // scheme used on real JMA intensity maps.
        public static string GetColorHex(string intensity)
        {
            int rank = ToRank(intensity);
            if (rank >= 9) return "9C27B0";      // 7 - purple
            if (rank >= 7) return "F44336";      // 6弱/6強 - red
            if (rank >= 5) return "FF9800";      // 5弱/5強 - orange
            if (rank >= 3) return "FFC107";      // 3/4 - amber
            if (rank >= 1) return "4CAF50";      // 1/2 - green
            return "9E9E9E";                      // unknown - gray
        }
    }
}
