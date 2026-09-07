using UnityEngine;

namespace EarthquakeGame
{
    // Keeps a sky-gradient sprite exactly filling the camera view, however
    // far the camera has zoomed out to follow the tower. Lives as a child
    // of the camera so it never drifts or shows an edge.
    [ExecuteAlways]
    public class CameraBackdrop : MonoBehaviour
    {
        public SpriteRenderer backdrop;

        void LateUpdate()
        {
            if (backdrop == null) backdrop = GetComponent<SpriteRenderer>();
            if (backdrop == null || backdrop.sprite == null) return;

            Camera cam = GetComponentInParent<Camera>();
            if (cam == null || !cam.orthographic) return;

            float viewHeight = cam.orthographicSize * 2f;
            float viewWidth = viewHeight * cam.aspect;
            Vector2 spriteSize = backdrop.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            transform.localScale = new Vector3(viewWidth / spriteSize.x, viewHeight / spriteSize.y, 1f);
        }
    }
}
