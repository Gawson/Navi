using Microsoft.Data.Sqlite;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GpsPreview.Maps
{
	public class VectorTile
	{
		public int id { get; private set; }
		public int zoomLevel { get; private set; }
		public int tile_column { get; private set; }
		public int tile_row { get; private set; }
		public byte[] tile_data_raw { get; private set; }

		public static VectorTile Parse(SqliteDataReader data)
		{
			var vectorTile = new VectorTile();
			while (data.Read())
			{
				for (int it = 0; it < data.FieldCount; it++)
				{
					switch (data.GetName(it))
					{
						case "id": vectorTile.id = data.GetFieldValue<int>(it); break;
						case "zoomLevel": vectorTile.zoomLevel = data.GetFieldValue<int>(it); break;
						case "tile_column": vectorTile.tile_column = data.GetFieldValue<int>(it); break;
						case "tile_row": vectorTile.tile_row = data.GetFieldValue<int>(it); break;
						case "tile_data": vectorTile.tile_data_raw = MapUtil.DecompressBinaryData(data.GetFieldValue<byte[]>(it)); break;
					}
				}
			}
			return vectorTile;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"id = {id}");
			sb.AppendLine($"zoomLevel = {zoomLevel}");
			sb.AppendLine($"tile_column = {tile_column}");
			sb.AppendLine($"tile_row = {tile_row}");
			sb.AppendLine($"tile_data = byte[]");
			return sb.ToString();
		}
	}

	public class PBF
	{
	
		public PBF(byte[] rawData)
		{
			//BinaryReader binaryReader = new BinaryReader(new MemoryStream(rawData));
			//var blobHeaderLength = binaryReader.ReadInt64();

			//Stopwatch stopwatch = new Stopwatch();
			//stopwatch.Start();
			//for (int it = 0; it < 100; it++)
			//{
				var header = Serializer.Deserialize<Tile>(new MemoryStream(rawData));
			//}
			//stopwatch.Stop();

			//Console.WriteLine(stopwatch.ElapsedMilliseconds);
		}
		
	}
}
