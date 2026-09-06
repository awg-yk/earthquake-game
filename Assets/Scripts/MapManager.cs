using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EarthquakeGame
{
    // Shows only the player's current prefecture and its direct neighbors
    // (rebuilt every time the player moves), instead of all 46 at once.
    // This intentionally skips a hand-drawn/geographic map image - the
    // spec asks to prioritize the game system over visuals for now.
    public class MapManager : MonoBehaviour
    {
        [Tooltip("Prefab with a Button + Text/TMP child, used for each prefecture.")]
        public Button prefectureButtonPrefab;

        [Tooltip("Parent transform (e.g. a Grid Layout Group) that buttons are instantiated under.")]
        public Transform buttonContainer;

        public Color currentColor = new Color(1f, 0.6f, 0.2f); // orange
        public Color reachableColor = new Color(0.6f, 1f, 0.6f); // light green
        public Color notYetReachableColor = new Color(0.75f, 0.75f, 0.75f); // gray

        private GameManager gameManager;

        public void Init(GameManager owner, IEnumerable<string> allPrefectures)
        {
            gameManager = owner;
            // allPrefectures is no longer used to pre-build every button -
            // Refresh() builds only the current + neighboring prefectures.
        }

        public void Refresh(string currentPrefecture, List<string> neighbors, bool canMove)
        {
            foreach (Transform child in buttonContainer)
            {
                Destroy(child.gameObject);
            }

            CreateButton(currentPrefecture, currentColor, false);

            foreach (var neighbor in neighbors)
            {
                CreateButton(neighbor, canMove ? reachableColor : notYetReachableColor, canMove);
            }
        }

        private void CreateButton(string prefectureName, Color color, bool interactable)
        {
            Button btn = Instantiate(prefectureButtonPrefab, buttonContainer);
            btn.gameObject.SetActive(true);
            btn.interactable = interactable;

            var label = btn.GetComponentInChildren<Text>();
            if (label != null) label.text = prefectureName;

            var image = btn.GetComponent<Image>();
            if (image != null) image.color = color;

            if (interactable)
            {
                string captured = prefectureName;
                btn.onClick.AddListener(() => gameManager.OnPrefectureClicked(captured));
            }
        }
    }
}
