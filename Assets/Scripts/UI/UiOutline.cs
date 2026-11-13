using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBreaker
{
    /// <summary>
    /// Chunky text outline: copies the glyphs in a ring behind the text plus a few lower copies, so the label gets
    /// the dark rim and the small "extrude" under it that the button and panel art have.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UiOutline : BaseMeshEffect
    {
        public Color color = new Color(0.133f, 0.07f, 0.247f, 1f);
        public float width = 2.5f;
        /// <summary>How far the lower copies drop below the ring (0 = plain outline).</summary>
        public float drop = 2.5f;

        static readonly Vector2[] Ring =
        {
            new Vector2(1f, 0f), new Vector2(0.707f, 0.707f), new Vector2(0f, 1f), new Vector2(-0.707f, 0.707f),
            new Vector2(-1f, 0f), new Vector2(-0.707f, -0.707f), new Vector2(0f, -1f), new Vector2(0.707f, -0.707f)
        };
        static readonly List<UIVertex> Source = new List<UIVertex>();
        static readonly List<UIVertex> Result = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
                return;
            Source.Clear();
            Result.Clear();
            vh.GetUIVertexStream(Source);
            if (drop > 0f)
            {
                // Lower three directions again, pushed down: the extrude.
                for (int i = 5; i < 8; i++)
                    Copy(Ring[i] * width + new Vector2(0f, -drop));
            }
            for (int i = 0; i < Ring.Length; i++)
                Copy(Ring[i] * width);
            Result.AddRange(Source);
            vh.Clear();
            vh.AddUIVertexTriangleStream(Result);
        }

        void Copy(Vector2 offset)
        {
            for (int i = 0; i < Source.Count; i++)
            {
                var vertex = Source[i];
                var position = vertex.position;
                position.x += offset.x;
                position.y += offset.y;
                vertex.position = position;
                var tint = (Color32)color;
                tint.a = (byte)(tint.a * vertex.color.a / 255);
                vertex.color = tint;
                Result.Add(vertex);
            }
        }
    }
}
