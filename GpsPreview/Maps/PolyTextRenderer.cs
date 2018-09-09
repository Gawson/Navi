using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace GpsPreview.Maps
{
	public class PolyTextRenderer : ICanvasTextRenderer
	{
		CanvasDrawingSession drawingSession;
		ICanvasBrush defaultBrush;

		public float LinearPosition { get; set; } = 0;
		public float Spacing { get; set; } = 1;

		private Vector2[] points;
		private float[] dists;
		public Vector2[] Poly { set
			{
				points = value;
				if (value == null)
				{
					dists = null;
					return;
				}

				dists = new float[points.Length];
				dists[0] = 0;
				for(int it = 1; it < points.Length;it++)
				{
					dists[it] = dists[it-1] + Vector2.Distance(points[it-1], points[it]);
				}
			}
		}

		public PolyTextRenderer(CanvasDrawingSession drawingSession)
		{
			this.drawingSession = drawingSession;
			this.defaultBrush = new CanvasSolidColorBrush(drawingSession, Colors.Black);
		}

		private (Vector2 pos, float a) PositionAngleOnPoly(float linearPositionOffset)
		{
			for(int it = 1; it < dists.Length; it++)
			{
				if((LinearPosition + linearPositionOffset) < dists[it])
				{
					var d1 = dists[it - 1];
					var d2 = dists[it];
					var segmentLength = d2 - d1;
					var segmentOffset = ((LinearPosition + linearPositionOffset) - d1) / segmentLength;
					var position = Vector2.Lerp(points[it - 1], points[it], segmentOffset);
					var v = points[it] - points[it - 1];
					var angle = MathF.Atan2(v.Y, v.X);
					return (position, angle);
				}
			}
			return (Vector2.Zero, 0);
		}



		public void DrawGlyphRun(Vector2 point, CanvasFontFace fontFace, float fontSize, CanvasGlyph[] glyphs, bool isSideways, uint bidiLevel, object brush, CanvasTextMeasuringMode measuringMode, string localeName, string textString, int[] clusterMapIndices, uint characterIndex, CanvasGlyphOrientation glyphOrientation)
		{

			if (points == null)
			{
				Vector2 adv = Vector2.Zero;

				for (int it = 0; it < glyphs.Length; it++)
				{
					var previousTransform = drawingSession.Transform;
					var drawPoint = point + adv;
					var rotationPoint = point + adv + new Vector2(glyphs[it].Advance / 2f, -fontSize / 4f);

					drawingSession.Transform = Matrix3x2.CreateRotation(it * 0.01f, rotationPoint);
					drawingSession.DrawCircle(point + adv + rotationPoint, 0.022f, Colors.Red);
					drawingSession.DrawGlyphRun(
						drawPoint,
						fontFace,
						fontSize,
						new CanvasGlyph[] { glyphs[it] },
						isSideways,
						bidiLevel,
						defaultBrush);
					drawingSession.Transform = previousTransform;
					adv += new Vector2(glyphs[it].Advance * Spacing, 0);
				}
			}
			else
			{
				float textLength = 0;
				foreach (var g in glyphs)
					textLength += g.Advance;
				if (textLength * 3f > dists.Last()) return;


				var labelCount = MathF.Truncate(dists.Last() / (textLength * 3));
				labelCount = MathF.Truncate(MathF.Min(labelCount, MathF.Sqrt(labelCount)));
				var labelSegmentLength = dists.Last() / labelCount;

				// DEBUG
				//CanvasPathBuilder path = new CanvasPathBuilder(drawingSession);
				//path.BeginFigure(points[0]);
				//points.ToList().ForEach(p => path.AddLine(p));
				//path.EndFigure(CanvasFigureLoop.Open);
				//drawingSession.DrawGeometry(CanvasGeometry.CreatePath(path), Colors.Blue, 0.1f);
				// =====



				for (int label = 0; label < labelCount; label++)
				{

					LinearPosition = labelSegmentLength * label + (labelSegmentLength / 2f) - (textLength / 2f);

					Vector2 adv = Vector2.Zero;

					for (int it = 0; it < glyphs.Length; it++)
					{
						var online = PositionAngleOnPoly(adv.X);

						var drawPoint = online.pos;
						var c = fontSize / 4f;
						var ox = MathF.Cos(online.a + MathF.PI / 2f) * c;
						var oy = MathF.Sin(online.a + MathF.PI / 2f) * c;
						drawPoint += new Vector2(ox, oy);

						var rotationPoint = drawPoint;// + new Vector2(glyphs[it].Advance / 2f, -fontSize / 4f);

						//drawingSession.DrawCircle(rotationPoint, 0.022f, Colors.Red);

						var previousTransform = drawingSession.Transform;
						drawingSession.Transform = Matrix3x2.CreateRotation(online.a, rotationPoint);
						drawingSession.DrawGlyphRun(
							drawPoint,
							fontFace,
							fontSize,
							new CanvasGlyph[] { glyphs[it] },
							isSideways,
							bidiLevel,
							defaultBrush);
						drawingSession.Transform = previousTransform;
						adv += new Vector2(glyphs[it].Advance * Spacing, 0);
					}
				}
			}

			//drawingSession.DrawGlyphRun(point, fontFace, fontSize, glyphs, isSideways, bidiLevel, defaultBrush);
		}

		public void DrawStrikethrough(Vector2 point, float strikethroughWidth, float strikethroughThickness, float strikethroughOffset, CanvasTextDirection textDirection, object brush, CanvasTextMeasuringMode textMeasuringMode, string localeName, CanvasGlyphOrientation glyphOrientation)
		{
			//throw new NotImplementedException();
			;
		}

		public void DrawUnderline(Vector2 point, float underlineWidth, float underlineThickness, float underlineOffset, float runHeight, CanvasTextDirection textDirection, object brush, CanvasTextMeasuringMode textMeasuringMode, string localeName, CanvasGlyphOrientation glyphOrientation)
		{
			//throw new NotImplementedException();
			;
		}

		public void DrawInlineObject(Vector2 point, ICanvasTextInlineObject inlineObject, bool isSideways, bool isRightToLeft, object brush, CanvasGlyphOrientation glyphOrientation)
		{
			//throw new NotImplementedException();
			;
		}

		public float Dpi => throw new NotImplementedException();

		public bool PixelSnappingDisabled => true;

		public Matrix3x2 Transform => Matrix3x2.Identity;
	}
}
