using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // Phase 1 map: one button per prefecture, arranged in a scroll view.
    // This intentionally skips a hand-drawn/geographic map image - the
    // spec asks to prioritize the game system over visuals for now.
    // Clicking a button asks GameManager to move there.
    public class MapManager : MonoBehaviour
    {
        [Tooltip("Prefab with a Button + Text/TMP child, used for each prefecture.")]
        public Button prefectureButtonPrefab;

        [Tooltip("Parent transform (e.g. a Grid Layout Group) that buttons are instantiated under.")]
        public Transform buttonContainer;

        public Color normalColor = Color.white;
        public Color currentColor = new Color(1f, 0.6f, 0.2f); // orange
        public Color reachableColor = new Color(0.6f, 1f, 0.6f); // light green

        private Dictionary<string, Button> buttonsByPrefecture = new Dictionary<string, Button>();
        private GameManager gameManager;

        public void Init(GameManager owner, IEnumerable<string> allPrefectures)
        {
            gameManager = owner;
            foreach (Transform child in buttonContainer)
            {
                Destroy(child.gameObject);
            }
            buttonsByPrefecture.Clear();

            foreach (var name in allPrefectures)
            {
                Button btn = Instantiate(prefectureButtonPrefab, buttonContainer);
                btn.gameObject.SetActive(true);
                var label = btn.GetComponentInChildren<Text>();
                if (label != null) label.text = name;
                string captured = name;
                btn.onClick.AddListener(() => gameManager.OnPrefectureClicked(captured));
                buttonsByPrefecture[name] = btn;
            }
        }

        public void Refresh(string currentPrefecture, List<string> reachablePrefectures)
        {
            var reachableSet = new HashSet<string>(reachablePrefectures);
            foreach (var kv in buttonsByPrefecture)
            {
                var image = kv.Value.GetComponent<Image>();
                if (image == null) continue;

                if (kv.Key == currentPrefecture)
                {
                    image.color = currentColor;
                }
                else if (reachableSet.Contains(kv.Key))
                {
                    image.color = reachableColor;
                }
                else
                {
                    image.color = normalColor;
                }
            }
        }
    }
}
