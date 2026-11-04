using UnityEngine;
using SSRP.Core;

namespace SSRP.Arena
{
    /// <summary>
    /// [D4] Fits an orthographic camera to arena bounds + margin for ANY aspect ratio:
    ///   size = max(height / 2, (width / 2) / aspect) + margin
    /// Re-fits automatically when the window aspect changes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ArenaCameraFitter : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float extraMargin = 0.3f;

        private Camera cam;
        private ArenaBounds bounds;
        private float margin;
        private bool hasBounds;
        private float lastAspect = -1f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        public void Fit(ArenaBounds arenaBounds, float arenaMargin)
        {
            bounds = arenaBounds;
            margin = arenaMargin;
            hasBounds = true;
            Apply();
        }

        private void LateUpdate()
        {
            if (hasBounds && !Mathf.Approximately(cam.aspect, lastAspect))
                Apply();
        }

        private void Apply()
        {
            if (cam == null)
                cam = GetComponent<Camera>();

            lastAspect = cam.aspect;
            cam.orthographicSize = ComputeOrthographicSize(bounds, margin + extraMargin, cam.aspect);

            Vector2 centre = (bounds.Min + bounds.Max) * 0.5f;
            transform.position = new Vector3(centre.x, centre.y, transform.position.z);
        }

        public static float ComputeOrthographicSize(ArenaBounds b, float margin, float aspect)
        {
            float halfWidth = (b.Max.x - b.Min.x) * 0.5f + margin;
            float halfHeight = (b.Max.y - b.Min.y) * 0.5f + margin;
            return Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.01f, aspect));
        }
    }
}
