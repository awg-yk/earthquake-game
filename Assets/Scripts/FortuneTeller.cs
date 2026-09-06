using System;
using UnityEngine;

namespace EarthquakeGame
{
    // The in-game "fortune teller" (占い師). Not real earthquake prediction -
    // it peeks at future earthquake data the player cannot see directly, and
    // turns it into a vague hint (rough direction, rough timeframe) so the
    // player can make an informed-but-uncertain decision about where to move.
    public class FortuneTeller : MonoBehaviour
    {
        public EarthquakeManager earthquakeManager;
        public PlayerManager playerManager;

        [Tooltip("How many days ahead the fortune teller is allowed to peek.")]
        public int lookAheadDays = 10;

        public string GetFortune(DateTime currentDate, string currentPrefecture)
        {
            EarthquakeEvent futureEvent = earthquakeManager.PeekFutureEarthquake(currentDate, lookAheadDays);
            if (futureEvent == null)
            {
                return "占い師：しばらくは静かな日々が続きそうです。";
            }

            DateTime eventDate = DateTime.Parse(futureEvent.date);
            int daysUntil = (eventDate - currentDate).Days;

            var (playerLat, playerLon) = playerManager.GetCoordinates(currentPrefecture);
            string direction = GetCompassDirection(playerLat, playerLon, futureEvent.latitude, futureEvent.longitude);

            if (direction == null)
            {
                return $"占い師：{daysUntil}日以内に、大きな地震が発生する可能性があります。";
            }

            return $"占い師：{daysUntil}日以内に、あなたのいる場所から{direction}の方向で大きな揺れが起きるかもしれません。";
        }

        // Very rough 8-direction compass bearing from (fromLat,fromLon) to (toLat,toLon).
        // Precision doesn't matter here - this is flavor text, not navigation.
        private static string GetCompassDirection(double fromLat, double fromLon, double toLat, double toLon)
        {
            double dLat = toLat - fromLat;
            double dLon = toLon - fromLon;

            if (Math.Abs(dLat) < 0.05 && Math.Abs(dLon) < 0.05) return null; // essentially on top of the player

            double angle = Math.Atan2(dLon, dLat) * Mathf.Rad2Deg; // 0 = north, 90 = east
            if (angle < 0) angle += 360;

            string[] directions = { "北", "北東", "東", "南東", "南", "南西", "西", "北西" };
            int index = Mathf.RoundToInt((float)angle / 45f) % 8;
            return directions[index];
        }
    }
}
