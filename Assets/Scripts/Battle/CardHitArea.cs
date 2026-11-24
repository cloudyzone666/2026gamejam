using UnityEngine;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>Invisible broad phase rectangle plus the card's exact projected polygon filter.</summary>
    [DisallowMultipleComponent]
    public sealed class CardHitArea : MaskableGraphic, ICanvasRaycastFilter
    {
        private CardPresentation presentation;

        internal void Initialize(CardPresentation owner)
        {
            presentation = owner;
            color = Color.clear;
            raycastTarget = true;
            // Keep this transparent mesh in Canvas depth ordering for GraphicRaycaster.
            canvasRenderer.cullTransparentMesh = false;
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = Color.clear;
            vertex.position = new Vector3(rect.xMin, rect.yMin); helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMin, rect.yMax); helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMax); helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMin); helper.AddVert(vertex);
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            return presentation != null && presentation.ContainsScreenPoint(screenPoint);
        }
    }
}
