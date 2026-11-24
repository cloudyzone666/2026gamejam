using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CardCards.Battle
{
    /// <summary>Projects uGUI vertices without changing the card's serialized layout or pose.</summary>
    [DisallowMultipleComponent]
    public sealed class CardPerspectiveEffect : BaseMeshEffect
    {
        private readonly List<UIVertex> source = new List<UIVertex>(256);
        private readonly UIVertex[] quad = new UIVertex[4];
        private CardPresentation presentation;
        private bool perspective;
        private bool subdivide;
        private Matrix4x4 lastRelativeMatrix;
        private Rect lastRect;

        internal void Initialize(CardPresentation owner, bool usePerspective)
        {
            presentation = owner;
            perspective = usePerspective;
            subdivide = usePerspective && graphic is Image;
            SetVerticesDirty();
        }

        internal void SetVerticesDirty()
        {
            if (graphic != null) graphic.SetVerticesDirty();
        }

        internal void RefreshTransform()
        {
            if (presentation == null || graphic == null) return;
            Matrix4x4 relative = presentation.WorldToRoot * transform.localToWorldMatrix;
            Rect rect = graphic.rectTransform.rect;
            if (relative != lastRelativeMatrix || rect != lastRect)
            {
                lastRelativeMatrix = relative;
                lastRect = rect;
                SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || presentation == null || !presentation.isActiveAndEnabled || helper.currentVertCount == 0) return;
            Matrix4x4 toRoot = presentation.WorldToRoot * transform.localToWorldMatrix;
            Matrix4x4 fromRoot = transform.worldToLocalMatrix * presentation.RootToWorld;
            if (!subdivide)
            {
                UIVertex vertex = default(UIVertex);
                for (int i = 0; i < helper.currentVertCount; i++)
                {
                    helper.PopulateUIVertex(ref vertex, i);
                    vertex.position = Project(vertex.position, toRoot, fromRoot);
                    helper.SetUIVertex(vertex, i);
                }
                return;
            }

            source.Clear();
            helper.GetUIVertexStream(source);
            helper.Clear();
            for (int i = 0; i < source.Count;)
            {
                // Image simple/sliced/filled rectangles use two adjacent triangles per quad.
                // Subdivision keeps the perspective texture smooth across their diagonal.
                bool isQuad = i + 5 < source.Count
                    && (source[i].position - source[i + 5].position).sqrMagnitude < 0.0001f
                    && (source[i + 2].position - source[i + 3].position).sqrMagnitude < 0.0001f;
                if (isQuad)
                {
                    AddSubdividedQuad(helper, source[i], source[i + 1], source[i + 2], source[i + 4], toRoot, fromRoot);
                    i += 6;
                }
                else
                {
                    // Nonrectangular filled/sprite meshes preserve their original triangles.
                    int first = helper.currentVertCount;
                    for (int j = 0; j < 3 && i + j < source.Count; j++)
                    {
                        UIVertex vertex = source[i + j];
                        vertex.position = Project(vertex.position, toRoot, fromRoot);
                        helper.AddVert(vertex);
                    }
                    helper.AddTriangle(first, first + 1, first + 2);
                    i += 3;
                }
            }
        }

        private Vector3 Project(Vector3 point, Matrix4x4 toRoot, Matrix4x4 fromRoot)
        {
            return fromRoot.MultiplyPoint3x4(presentation.ProjectRootPoint(toRoot.MultiplyPoint3x4(point), perspective));
        }

        private void AddSubdividedQuad(VertexHelper helper, UIVertex a, UIVertex b, UIVertex c, UIVertex d,
            Matrix4x4 toRoot, Matrix4x4 fromRoot)
        {
            // Eight columns and two rows are sufficient for these small UI images; Text keeps
            // its existing per-glyph quads so no font atlas geometry is merged or re-created.
            const int columns = 8;
            const int rows = 2;
            for (int y = 0; y < rows; y++)
            {
                float v0 = (float)y / rows;
                float v1 = (float)(y + 1) / rows;
                UIVertex left0 = Lerp(a, b, v0);
                UIVertex right0 = Lerp(d, c, v0);
                UIVertex left1 = Lerp(a, b, v1);
                UIVertex right1 = Lerp(d, c, v1);
                for (int x = 0; x < columns; x++)
                {
                    float u0 = (float)x / columns;
                    float u1 = (float)(x + 1) / columns;
                    quad[0] = Lerp(left0, right0, u0);
                    quad[1] = Lerp(left1, right1, u0);
                    quad[2] = Lerp(left1, right1, u1);
                    quad[3] = Lerp(left0, right0, u1);
                    for (int n = 0; n < 4; n++) quad[n].position = Project(quad[n].position, toRoot, fromRoot);
                    helper.AddUIVertexQuad(quad);
                }
            }
        }

        private static UIVertex Lerp(UIVertex a, UIVertex b, float t)
        {
            UIVertex vertex = a;
            vertex.position = Vector3.LerpUnclamped(a.position, b.position, t);
            vertex.normal = Vector3.LerpUnclamped(a.normal, b.normal, t);
            vertex.tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t);
            vertex.color = Color32.LerpUnclamped(a.color, b.color, t);
            vertex.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
            vertex.uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t);
            vertex.uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t);
            vertex.uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t);
            return vertex;
        }
    }
}
