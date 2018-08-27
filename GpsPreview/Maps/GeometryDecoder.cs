using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System.Collections.Generic;
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


		public static Dictionary<string, string> GetTags(Tile.Feature feature, Tile.Layer layer)
		{
			Dictionary<string, string> tags = new Dictionary<string, string>();
			if (feature.Tags == null) return tags;
			Queue<uint> q = new Queue<uint>(feature.Tags);
			while(q.Count>0)
			{
				tags.Add(layer.Keys[(int)q.Dequeue()], layer.Values[(int)q.Dequeue()].StringValue);
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
							session.FillGeometry(geom, fillColor);
							session.DrawGeometry(geom, strokeColor);
						}
						poly.Clear();
						break;
				}
			}
			if (feature.Type == Tile.GeomType.Linestring)
			{
				for (int it = 0; it < (poly.Count - 1); it++)
				{
					session.DrawLine(poly[it], poly[it + 1], strokeColor, outerLineWidth);
					session.DrawLine(poly[it], poly[it + 1], fillColor, innerLineWidth);
				}
				poly.Clear();
			}
		}
	}
}
