using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GpsPreview.Maps
{
	
	class MapUtil
	{
		public static PointF WorldToTilePos(double lon, double lat, int zoom)
		{
			PointF p = new Point();
			p.X = (float)((lon + 180.0) / 360.0 * (1 << zoom));
			p.Y = (float)((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) +
				1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * (1 << zoom));

			return p;
		}

		public static PointF TileToWorldPos(double tile_x, double tile_y, int zoom)
		{
			PointF p = new Point();
			double n = Math.PI - ((2.0 * Math.PI * tile_y) / Math.Pow(2.0, zoom));

			p.X = (float)((tile_x / Math.Pow(2.0, zoom) * 360.0) - 180.0);
			p.Y = (float)(180.0 / Math.PI * Math.Atan(Math.Sinh(n)));

			return p;
		}

		public static string DecompressData(byte[] data)
		{
			System.IO.Compression.GZipStream gZipStream = new System.IO.Compression.GZipStream(new MemoryStream(data), System.IO.Compression.CompressionMode.Decompress);
			StreamReader streamReader = new StreamReader(gZipStream);
			return streamReader.ReadToEnd();
		}

		public static byte[] DecompressBinaryData(byte[] data)
		{
			System.IO.Compression.GZipStream gZipStream = new System.IO.Compression.GZipStream(new MemoryStream(data), System.IO.Compression.CompressionMode.Decompress);
			BinaryReader streamReader = new BinaryReader(gZipStream);
			return streamReader.ReadBytes(1*1024*1024);
		}
	}
}
