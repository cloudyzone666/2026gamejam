using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>
    /// Shares one projection between every graphic and the pointer hit area of a card.
    /// The root RectTransform remains a stable layout/drag anchor; CardVisual can still turn.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CardPresentation : MonoBehaviour
    {
        private readonly List<Graphic> graphics = new List<Graphic>(32);
        private readonly List<CardPerspectiveEffect> effects = new List<CardPerspectiveEffect>(32);
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);
        private readonly Vector3[] worldCorners = new Vector3[4];
        private readonly Vector3[] sourceCorners = new Vector3[4];
        private readonly Vector3[] hoverEntryCorners = new Vector3[4];
        private RectTransform root;
        private RectTransform cardVisual;
        private CardHitArea hitArea;
        private Canvas parentCanvas;
        private PointerEventData samplePointer;
        private EventSystem sampleEventSystem;
        // These values are copied into the existing drag-ghost Instantiate path, while all
        // owner/cache references below are rebuilt against the cloned hierarchy.
        [SerializeField, HideInInspector] private float yaw;
        [SerializeField, HideInInspector] private float perspectiveDistance = 600f;
        [SerializeField, HideInInspector] private Vector2 perspectiveCenterOffset;
        [SerializeField, HideInInspector] private float verticalOffset;
        [SerializeField, HideInInspector] private float hoverScale = 1f;
        private float sine;
        private float cosine = 1f;
        private bool hierarchyDirty = true;
        private bool presentationDirty = true;
        private bool interactionEnabled = true;
        private bool hasHoverEntry;

        public bool IsHovered { get; private set; }
        public bool IsDragging { get; private set; }

        private void Awake() { EnsureReferences(); }

        private void OnEnable()
        {
            EnsureReferences();
            sine = Mathf.Sin(yaw * Mathf.Deg2Rad);
            cosine = Mathf.Cos(yaw * Mathf.Deg2Rad);
            hierarchyDirty = true;
            presentationDirty = true;
        }

        private void OnDisable()
        {
            IsHovered = false;
            IsDragging = false;
            hasHoverEntry = false;
            if (hitArea != null) hitArea.raycastTarget = false;
            MarkGraphicsDirty();
        }

        private void OnTransformChildrenChanged() { hierarchyDirty = true; }

        public void Configure(float yawDegrees, float distance, Vector2 centerOffset,
            float floatOffset, float hoverLift, float scale)
        {
            EnsureReferences();
            float nextYaw = Mathf.Clamp(yawDegrees, -75f, 75f);
            float nextDistance = Mathf.Max(100f, distance);
            float nextScale = Mathf.Max(0.1f, scale);
            float nextVertical = floatOffset + hoverLift;
            if (yaw != nextYaw || perspectiveDistance != nextDistance
                || perspectiveCenterOffset != centerOffset || verticalOffset != nextVertical
                || hoverScale != nextScale)
            {
                yaw = nextYaw;
                perspectiveDistance = nextDistance;
                perspectiveCenterOffset = centerOffset;
                verticalOffset = nextVertical;
                hoverScale = nextScale;
                sine = Mathf.Sin(yaw * Mathf.Deg2Rad);
                cosine = Mathf.Cos(yaw * Mathf.Deg2Rad);
                presentationDirty = true;
            }
            // Newly enabled/spawned cards must be hittable before the next EventSystem update.
            if (hierarchyDirty) RebuildGraphicCache();
            UpdateHitArea();
        }

        public void SetPointerState(bool hovered, bool dragging)
        {
            if (hovered && !IsHovered)
            {
                // Keep this in root space so the retention area follows live layout edits.
                // It is deliberately independent from the exact, visible click polygon.
                Vector3[] corners = GetWorldCorners();
                for (int i = 0; i < 4; i++) hoverEntryCorners[i] = root.InverseTransformPoint(corners[i]);
                hasHoverEntry = true;
            }
            else if (!hovered) hasHoverEntry = false;
            IsHovered = hovered;
            IsDragging = dragging;
        }

        /// <summary>
        /// Keeps hover stable while a raised card moves away from an entering edge.
        /// Pointer clicks continue to use only ContainsScreenPoint's visible polygon.
        /// </summary>
        public bool RetainsHover(Vector2 screenPoint)
        {
            if (!IsHovered || IsDragging || !isActiveAndEnabled || !interactionEnabled) return false;
            return ContainsScreenPoint(screenPoint)
                || (hasHoverEntry && ContainsPolygon(screenPoint, hoverEntryCorners, true));
        }

        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
            EnsureReferences();
            if (hierarchyDirty) RebuildGraphicCache();
            if (hitArea != null) hitArea.raycastTarget = enabled && isActiveAndEnabled;
            for (int i = 0; i < graphics.Count; i++)
                if (graphics[i] != null && graphics[i] != hitArea) graphics[i].raycastTarget = false;
        }

        private void EnsureReferences()
        {
            if (root == null) root = (RectTransform)transform;
            if (cardVisual == null) cardVisual = transform.Find("CardVisual") as RectTransform;
            if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
            if (hitArea != null) return;
            Transform existing = transform.Find("CardPresentationHitArea");
            if (existing != null) hitArea = existing.GetComponent<CardHitArea>();
            if (hitArea == null)
            {
                GameObject hitObject = new GameObject("CardPresentationHitArea",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(CardHitArea));
                hitObject.layer = gameObject.layer;
                hitObject.transform.SetParent(transform, false);
                hitArea = hitObject.GetComponent<CardHitArea>();
            }
            hitArea.Initialize(this);
        }

        private void RebuildGraphicCache()
        {
            EnsureReferences();
            graphics.Clear();
            GetComponentsInChildren(true, graphics);
            effects.Clear();
            for (int i = 0; i < graphics.Count; i++)
            {
                Graphic item = graphics[i];
                if (item == hitArea) continue;
                item.raycastTarget = false;
                // The root Image is the Button's transparent target graphic, not card artwork.
                if (item.transform == transform) continue;
                CardPerspectiveEffect effect = item.GetComponent<CardPerspectiveEffect>();
                if (effect == null) effect = item.gameObject.AddComponent<CardPerspectiveEffect>();
                bool isCardArtwork = cardVisual != null
                    && (item.transform == cardVisual || item.transform.IsChildOf(cardVisual));
                effect.Initialize(this, isCardArtwork);
                effects.Add(effect);
            }
            if (hitArea.transform.GetSiblingIndex() != transform.childCount - 1)
                hitArea.transform.SetAsLastSibling();
            hierarchyDirty = false;
            presentationDirty = true;
        }

        private void LateUpdate()
        {
            if (hierarchyDirty) RebuildGraphicCache();
            if (presentationDirty)
            {
                MarkGraphicsDirty();
                presentationDirty = false;
            }
            // Rotation, floating damage labels and animated child transforms invalidate their
            // own meshes even when the layout/projection parameters did not change.
            for (int i = 0; i < effects.Count; i++)
                if (effects[i] != null) effects[i].RefreshTransform();
            UpdateHitArea();
        }

        private void MarkGraphicsDirty()
        {
            for (int i = 0; i < effects.Count; i++)
                if (effects[i] != null) effects[i].SetVerticesDirty();
        }

        internal Vector3 ProjectRootPoint(Vector3 point, bool perspective)
        {
            Vector3 result = point;
            if (perspective)
            {
                // Positive yaw brings the LEFT edge nearer; opposing teams use mirrored yaw.
                float depth = sine * point.x;
                float denominator = Mathf.Max(perspectiveDistance * 0.2f, perspectiveDistance + depth);
                float ratio = perspectiveDistance / denominator;
                result.x = perspectiveCenterOffset.x
                    + (point.x * cosine - perspectiveCenterOffset.x) * ratio;
                result.y = perspectiveCenterOffset.y
                    + (point.y - perspectiveCenterOffset.y) * ratio;
            }
            result.x *= hoverScale;
            result.y = result.y * hoverScale + verticalOffset;
            // UI remains on its original Canvas plane; depth is represented in x/y only.
            result.z = point.z;
            return result;
        }

        internal Matrix4x4 WorldToRoot => root.worldToLocalMatrix;
        internal Matrix4x4 RootToWorld => root.localToWorldMatrix;

        /// <summary>BL, TL, TR, BR, in world space. The same array is reused on each call.</summary>
        public Vector3[] GetWorldCorners()
        {
            EnsureReferences();
            RectTransform visual = cardVisual != null ? cardVisual : root;
            visual.GetWorldCorners(sourceCorners);
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = root.InverseTransformPoint(sourceCorners[i]);
                worldCorners[i] = root.TransformPoint(ProjectRootPoint(local, true));
            }
            return worldCorners;
        }

        private Camera EventCamera
        {
            get
            {
                if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
                return parentCanvas.worldCamera;
            }
        }

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (!isActiveAndEnabled || !interactionEnabled) return false;
            return ContainsPolygon(screenPoint, GetWorldCorners(), false);
        }

        private bool ContainsPolygon(Vector2 screenPoint, Vector3[] corners, bool rootLocal)
        {
            float previousCross = 0f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 cornerA = rootLocal ? root.TransformPoint(corners[i]) : corners[i];
                Vector3 cornerB = rootLocal ? root.TransformPoint(corners[(i + 1) % 4]) : corners[(i + 1) % 4];
                Vector2 a = RectTransformUtility.WorldToScreenPoint(EventCamera, cornerA);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(EventCamera, cornerB);
                float cross = (b.x - a.x) * (screenPoint.y - a.y)
                    - (b.y - a.y) * (screenPoint.x - a.x);
                if (Mathf.Abs(cross) < 0.001f) continue;
                if (previousCross != 0f && Mathf.Sign(cross) != Mathf.Sign(previousCross)) return false;
                previousCross = cross;
            }
            return previousCross != 0f;
        }

        /// <summary>Finds an exposed pointer position when neighbouring cards cover the centre.</summary>
        public Vector2 GetClickableScreenPoint()
        {
            Vector3[] corners = GetWorldCorners();
            Vector2 centre = RectTransformUtility.WorldToScreenPoint(EventCamera,
                (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f);
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return centre;
            if (samplePointer == null || sampleEventSystem != eventSystem)
            {
                sampleEventSystem = eventSystem;
                samplePointer = new PointerEventData(eventSystem);
            }
            if (IsExposed(centre, eventSystem)) return centre;
            // Prioritise the middle height, then the upper/lower portions; stay inside the border.
            for (int y = 0; y < 5; y++)
            {
                float ty = SampleFraction(y);
                for (int x = 0; x < 5; x++)
                {
                    float tx = SampleFraction(x);
                    Vector3 bottom = Vector3.Lerp(corners[0], corners[3], tx);
                    Vector3 top = Vector3.Lerp(corners[1], corners[2], tx);
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(EventCamera, Vector3.Lerp(bottom, top, ty));
                    if (IsExposed(point, eventSystem)) return point;
                }
            }
            return centre;
        }

        private static float SampleFraction(int index)
        {
            switch (index)
            {
                case 0: return 0.5f;
                case 1: return 0.08f;
                case 2: return 0.92f;
                case 3: return 0.25f;
                default: return 0.75f;
            }
        }

        private bool IsExposed(Vector2 point, EventSystem eventSystem)
        {
            samplePointer.position = point;
            raycastResults.Clear();
            eventSystem.RaycastAll(samplePointer, raycastResults);
            if (raycastResults.Count == 0) return false;
            Transform first = raycastResults[0].gameObject.transform;
            return first == transform || first.IsChildOf(transform);
        }

        private void UpdateHitArea()
        {
            if (hitArea == null) return;
            Vector3[] corners = GetWorldCorners();
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 4; i++)
            {
                Vector2 local = root.InverseTransformPoint(corners[i]);
                minimum = Vector2.Min(minimum, local);
                maximum = Vector2.Max(maximum, local);
            }
            RectTransform rect = hitArea.rectTransform;
            rect.anchorMin = rect.anchorMax = root.pivot;
            rect.pivot = new Vector2(0.5f, 0.5f);
            Vector2 nextPosition = (minimum + maximum) * 0.5f;
            Vector2 nextSize = maximum - minimum;
            if (rect.anchoredPosition != nextPosition) rect.anchoredPosition = nextPosition;
            if (rect.sizeDelta != nextSize) rect.sizeDelta = nextSize;
            hitArea.raycastTarget = interactionEnabled && isActiveAndEnabled;
        }
    }
}
