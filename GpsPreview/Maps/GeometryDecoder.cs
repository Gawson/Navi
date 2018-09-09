using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace GpsPreview.Maps
{
	public enum GeometryCommand
	{
		MoveTo = 1,
		LineTo = 2,
		ClosePath = 7
	}
	public class GeometryDecoder
	{
		public static CanvasRenderTarget offscreen = null;
		public static CanvasRenderTarget offscreenText = null;
		public static CanvasCommandList renderText = null;
		public static CanvasCommandList renderList = null;
		public static int CanvasTileId = -1;
		public static List<CanvasCachedGeometry> cache = new List<CanvasCachedGeometry>();

		public static void PurgeCache()
		{
			foreach (var c in cache) c.Dispose();
			cache.Clear();
		}

		public static CanvasStrokeStyle normalStrokeStyle = new CanvasStrokeStyle()
		{
			StartCap = CanvasCapStyle.Round,
			LineJoin = CanvasLineJoin.Round,
			EndCap = CanvasCapStyle.Round
		};

		public static CanvasStrokeStyle openStrokeStyle = new CanvasStrokeStyle()
		{
			StartCap = CanvasCapStyle.Flat,
			LineJoin = CanvasLineJoin.Round,
			EndCap = CanvasCapStyle.Flat
		};

		public static CanvasStrokeStyle railStrokeStyle = new CanvasStrokeStyle()
		{
			StartCap = CanvasCapStyle.Round,
			LineJoin = CanvasLineJoin.Round,
			EndCap = CanvasCapStyle.Round,
			CustomDashStyle = new float[] { 6, 6 },
			DashCap = CanvasCapStyle.Flat,
			DashOffset = 3
		};

		public static Dictionary<string, Tile.Value> GetTags(Tile.Feature feature, Tile.Layer layer)
		{
			Dictionary<string, Tile.Value> tags = new Dictionary<string, Tile.Value>();
			if (feature.Tags == null) return tags;
			Queue<uint> q = new Queue<uint>(feature.Tags);
			while(q.Count>0)
			{
				tags.Add(layer.Keys[(int)q.Dequeue()], layer.Values[(int)q.Dequeue()]);
			}

			return tags;
		}

		private static long DecodeParameter(uint parameter)
		{
			var value = ((parameter >> 1) ^ (-(parameter & 1)));
			//System.Diagnostics.Debug.WriteLine($"Param: {value}");
			return value;
		}
		private static (GeometryCommand command, uint count) DecodeCommand(uint command)
		{
			var commandId = command & 0x7;
			var paramCount = command >> 3;
			//System.Diagnostics.Debug.WriteLine($"Command: {commandId} ; count: {paramCount}");
			return ((GeometryCommand)commandId, paramCount);
		}

		public static Path DecodeGeometry(Tile.Feature feature, float scale)
		{
			//System.Diagnostics.Debug.WriteLine("----Start decode----");
			//System.Diagnostics.Debug.WriteLine("type: " + feature.Type);
			//System.Diagnostics.Debug.WriteLine("geometries: " + feature.Geometries.Length);

			Queue<uint> q = new Queue<uint>(feature.Geometries);
			Path path = new Path();
			Windows.UI.Xaml.Media.PathGeometry pathGeometry = new Windows.UI.Xaml.Media.PathGeometry();

			PathFigure figure = null;
			float cx = 0;
			float cy = 0;

			while(q.Count>0) {
				var cmd = DecodeCommand(q.Dequeue());
				switch (cmd.Item1)
				{
					case GeometryCommand.MoveTo:
						if(figure != null) { pathGeometry.Figures.Add(figure); }
						figure = new PathFigure();
						cx += DecodeParameter(q.Dequeue()) * scale;
						cy += DecodeParameter(q.Dequeue()) * scale;
						figure.StartPoint = new Windows.Foundation.Point(cx,cy);
						
						//System.Diagnostics.Debug.Write("MoveTo ");
						//for(int it=0; it<cmd.count*2;it++)
						//{
						//	System.Diagnostics.Debug.Write(DecodeParameter(q.Dequeue()) + " ");
						//}
						//System.Diagnostics.Debug.WriteLine("");
						break;
					case GeometryCommand.LineTo:
						for (int it = 0; it < cmd.count; it++)
						{
							cx += DecodeParameter(q.Dequeue()) * scale;
							cy += DecodeParameter(q.Dequeue()) * scale;
							figure.Segments.Add(new LineSegment() { Point = new Windows.Foundation.Point(cx,cy) });
						}
						
						//System.Diagnostics.Debug.Write("LineTo ");
						//for (int it = 0; it < cmd.count * 2; it++)
						//{
						//	System.Diagnostics.Debug.Write(DecodeParameter(q.Dequeue()) + " ");
						//}
						//System.Diagnostics.Debug.WriteLine("");
						break;
					case GeometryCommand.ClosePath:
						figure.Segments.Add(new LineSegment() { Point = figure.StartPoint });
						pathGeometry.Figures.Add(figure);
						figure.IsFilled = true;
						figure.IsClosed = true;
						figure = null;
						//System.Diagnostics.Debug.WriteLine("ClosePath");
						break;
				}					
			}

			if (figure != null) pathGeometry.Figures.Add(figure);

			path.Data = pathGeometry;
			path.Fill = new SolidColorBrush(Windows.UI.Colors.SandyBrown);
			path.Stroke = new SolidColorBrush(Windows.UI.Colors.Brown);
			path.StrokeThickness = 1;

				//System.Diagnostics.Debug.WriteLine("----Stop  decode----");
			return path;
		}

		public static void TestPerformance(Tile.Feature feature, float scale, Windows.UI.Color fillColor, Windows.UI.Color strokeColor, CanvasDrawingSession ds)
		{
			DrawGeometry(feature, scale, ds, fillColor, strokeColor);
		}

		public static void DrawGeometry(Tile.Feature feature, float scale, CanvasDrawingSession session, Windows.UI.Color fillColor, Windows.UI.Color strokeColor, float innerLineWidth = 1f, float outerLineWidth = 2f)
		{
			DrawGeometry(feature, scale, session, fillColor, strokeColor, innerLineWidth, outerLineWidth, normalStrokeStyle);
		}
		public static void DrawGeometry(Tile.Feature feature, float scale, CanvasDrawingSession session, Windows.UI.Color fillColor, Windows.UI.Color strokeColor, float innerLineWidth, float outerLineWidth, CanvasStrokeStyle strokeStyle)
		{
			Queue<uint> q = new Queue<uint>(feature.Geometries);
			float cx = 0;
			float cy = 0;

			List<System.Numerics.Vector2> poly = new List<System.Numerics.Vector2>();

			while (q.Count > 0)
			{
				var cmd = DecodeCommand(q.Dequeue());
				switch (cmd.Item1)
				{
					case GeometryCommand.MoveTo:
						cx += DecodeParameter(q.Dequeue()) * scale;
						cy += DecodeParameter(q.Dequeue()) * scale;
						poly.Add(new System.Numerics.Vector2(cx, cy));
						break;
					case GeometryCommand.LineTo:
						for (int it = 0; it < cmd.count; it++)
						{
							cx += DecodeParameter(q.Dequeue()) * scale;
							cy += DecodeParameter(q.Dequeue()) * scale;
							poly.Add(new System.Numerics.Vector2(cx, cy));
						}
						break;
					case GeometryCommand.ClosePath:
						if (feature.Type == Tile.GeomType.Polygon)
						{
							CanvasGeometry geom = CanvasGeometry.CreatePolygon(session.Device, poly.ToArray());
							var cf = CanvasCachedGeometry.CreateFill(geom);
							var cd = CanvasCachedGeometry.CreateStroke(geom, 1);
							cache.Add(cf); cache.Add(cd);
							session.DrawCachedGeometry(cf, fillColor);
							session.DrawCachedGeometry(cd, strokeColor);
							//session.FillGeometry(geom, fillColor);
							//session.DrawGeometry(geom, strokeColor);
						}
						poly.Clear();
						break;
				}
			}
			if (feature.Type == Tile.GeomType.Linestring)
			{
				CanvasPathBuilder pathBuilder = new CanvasPathBuilder(session);
				pathBuilder.SetSegmentOptions(CanvasFigureSegmentOptions.ForceRoundLineJoin);
				pathBuilder.BeginFigure(poly[0]);
				for (int it = 1; it < (poly.Count); it++)
				{
					pathBuilder.AddLine(poly[it]);
					
					//if(outerLineWidth > 0)
					//	session.DrawLine(poly[it], poly[it + 1], strokeColor, outerLineWidth, strokeStyle);
					//if (innerLineWidth > 0)
					//	session.DrawLine(poly[it], poly[it + 1], fillColor, innerLineWidth, strokeStyle);					
				}
				pathBuilder.EndFigure(CanvasFigureLoop.Open);
				var geometry = CanvasGeometry.CreatePath(pathBuilder);
				if (outerLineWidth > 0)
				{
					var cg = CanvasCachedGeometry.CreateStroke(geometry, outerLineWidth, strokeStyle);
					cache.Add(cg);
					session.DrawCachedGeometry(cg, strokeColor);
					//session.DrawGeometry(geometry, strokeColor, outerLineWidth, strokeStyle);
				}
				if (innerLineWidth > 0)
				{
					var cg = CanvasCachedGeometry.CreateStroke(geometry, innerLineWidth, strokeStyle);
					cache.Add(cg);
					session.DrawCachedGeometry(cg, fillColor);
					//session.DrawGeometry(geometry, fillColor, innerLineWidth, strokeStyle);
				}
				poly.Clear();
			}
		}

		internal static void ApplyTextLayer(CanvasDrawingSession session)
		{
			var shadow = new ShadowEffect()
			{
				BlurAmount = 0.01f,
				ShadowColor = Colors.White,
				Optimization = EffectOptimization.Quality
			};
			shadow.Source = renderText;
			session.DrawImage(shadow);
		}

		internal static void DrawText(
			Tile.Feature feature, 
			float scale, 
			CanvasDrawingSession session, 
			Color fillColor, 
			Color strokeColor, 
			int innerLineWidth, 
			float outerLineWidth, 
			string text, 
			float fontSize = 5f)
		{
			CanvasTextFormat textFormat = new CanvasTextFormat()
			{
				FontSize = fontSize,
				FontWeight = Windows.UI.Text.FontWeights.Normal,
				HorizontalAlignment = CanvasHorizontalAlignment.Center,
				VerticalAlignment = CanvasVerticalAlignment.Center
			};

			Queue<uint> q = new Queue<uint>(feature.Geometries);
			float cx = 0;
			float cy = 0;

			if (feature.Type == Tile.GeomType.Linestring)
			{	
				q = new Queue<uint>(feature.Geometries);
				cx = 0;
				cy = 0;

				List<Vector2> poly = new List<Vector2>();

				while (q.Count > 0)
				{
					var cmd = DecodeCommand(q.Dequeue());
					switch (cmd.Item1)
					{
						case GeometryCommand.MoveTo:
							cx += DecodeParameter(q.Dequeue()) * scale;
							cy += DecodeParameter(q.Dequeue()) * scale;
							poly.Add(new Vector2(cx, cy));
							break;
						case GeometryCommand.LineTo:
							for (int it = 0; it < cmd.count; it++)
							{
								cx += DecodeParameter(q.Dequeue()) * scale;
								cy += DecodeParameter(q.Dequeue()) * scale;
								poly.Add(new Vector2(cx, cy));
							}
							break;
					}
				}

				Vector2 start = Vector2.Zero;
				Vector2 end = Vector2.Zero;

				if (poly.Count %2 == 0)
				{
					var c = poly.Count / 2;
					start = poly[c-1];
					end = poly[c];

				} else
				{
					var c = poly.Count / 2f;
					start = poly[(int)Math.Floor(c-1)];
					end = poly[(int)Math.Ceiling(c-1)];
				}

				var mid = (start + end) / 2;
				var a = Math.Atan2((end - start).Y, (end - start).X);
				if (a > (Math.PI / 2f)) a -= Math.PI;

				//var oldTransform = session.Transform;
				//session.Transform = Matrix3x2.CreateRotation((float)a, mid);

				CanvasTextLayout textLayout = new CanvasTextLayout(session, text, textFormat, 100, 10);
				CanvasGeometry geometry = CanvasGeometry.CreateText(textLayout);
				PolyTextRenderer ptr = new PolyTextRenderer(session);
				ptr.Poly = poly.ToArray();
				textLayout.DrawToTextRenderer(ptr, mid);

				//session.DrawText(text, mid, fillColor, textFormat);
				//session.DrawTextLayout(textLayout, mid, fillColor);
				//session.DrawGeometry(geometry, mid, strokeColor, 0.1f);

				//session.Transform = oldTransform;

				//DrawGeometry(feature, scale, session, Colors.Red, Colors.Red, 1, 1);

			}

			if(feature.Type == Tile.GeomType.Point)
			{
				var cmd = DecodeCommand(q.Dequeue());
				cx += DecodeParameter(q.Dequeue()) * scale;
				cy += DecodeParameter(q.Dequeue()) * scale;

				var textLayout = new CanvasTextLayout(session, text, textFormat, 100, 10);
				Vector2 mid = new Vector2(cx, cy);
				CanvasGeometry geometry = CanvasGeometry.CreateText(textLayout);
				//session.DrawTextLayout(textLayout, mid, fillColor);
				//session.FillGeometry(geometry, mid, fillColor);
				//session.DrawGeometry(geometry, mid, strokeColor, 0.1f);
				//session.DrawRectangle(new Windows.Foundation.Rect(mid.X + textLayout.DrawBounds.X, mid.Y + textLayout.DrawBounds.Y, textLayout.DrawBounds.Width, textLayout.DrawBounds.Height), Colors.Red, 0.2f);
				session.DrawText(text, new Vector2(cx, cy), fillColor, textFormat);
			}
		}

		internal static void DrawIcon(Tile.Feature f, float scale, CanvasDrawingSession session, CanvasSvgDocument icon, Color black, Color darkGray)
		{
			float iconSize = 0.001f;
			Queue<uint> q = new Queue<uint>(f.Geometries);
			float cx = 0;
			float cy = 0;
			if (f.Type == Tile.GeomType.Point)
			{
				var cmd = DecodeCommand(q.Dequeue());
				cx += DecodeParameter(q.Dequeue()) * scale;
				cy += DecodeParameter(q.Dequeue()) * scale;
				var m = session.Transform;
				session.Transform = Matrix3x2.CreateScale(0.25f, new Vector2(cx, cy)) * Matrix3x2.CreateTranslation(-2,-2);
				session.DrawSvg(icon, new Windows.Foundation.Size(iconSize, iconSize), cx- iconSize/2f, cy - iconSize/2f);
				session.Transform = m;
				//session.DrawCircle(cx, cy, 0.1f, Colors.Red);
			}
		}
	}
}
