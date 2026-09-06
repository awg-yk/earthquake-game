using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthquakeGame
{
    // The in-game "earthquake forecaster" (地震予報士). Not real earthquake
    // prediction, but here it's deliberately confident and precise (not
    // vague): on the 1st of every month, it peeks at that entire month's
    // future earthquake data and marks every prefecture that will feel
    // shindo 2 or stronger at some point during the month - shown as
    // colors directly on the intensity map, not spelled out in words.
    public class FortuneTeller : MonoBehaviour
    {
        public EarthquakeManager earthquakeManager;

        [Tooltip("Minimum shindo (2 = shindo 2) the monthly forecast bothers marking on the map.")]
        public int minRank = 2;

        public string LastForecastMessage { get; private set; } = "";
        public Dictionary<string, string> LastForecastIntensities { get; private set; } = new Dictionary<string, string>();

        // Called on the 1st of every in-game month. Scans the whole month
        // for every prefecture that will feel shindo >= minRank at least
        // once, and builds both a text summary and a map-ready dictionary.
        public string GetMonthlyForecast(DateTime currentDate)
        {
            var map = earthquakeManager.GetPrefecturesWithQuakesInMonth(currentDate.Year, currentDate.Month, minRank);
            LastForecastIntensities = map;

            LastForecastMessage = map.Count > 0
                ? $"地震予報士：{currentDate.Month}月の予報まとめです"
                : $"地震予報士：{currentDate.Month}月は大きな揺れはなさそうです。";

            return LastForecastMessage;
        }
    }
}
