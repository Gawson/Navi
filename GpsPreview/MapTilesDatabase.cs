using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Windows.Storage;
using Windows.Storage.Compression;
using Windows.Storage.Streams;

namespace GpsPreview
{


	public class MapTilesDatabase
	{

		static string DecompressTile(byte[] tile_data)
		{
			System.IO.Compression.GZipStream gZipStream = new System.IO.Compression.GZipStream(new MemoryStream(tile_data), System.IO.Compression.CompressionMode.Decompress);
			StreamReader streamReader = new StreamReader(gZipStream);
			return streamReader.ReadToEnd();
		}

		static void ParseData(byte[] tile_data)
		{
			ParseData(DecompressTile(tile_data));
		}

		static void ParseData(string tile_data)
		{

		}


		static string dbFile = "Maps//2017-07-03_poland_warsaw.mbtiles";
		public static async Task<string> InitializeDatabase()
		{
			Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
			Windows.Storage.StorageFile databaseLocalFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///" + dbFile));

			using (SqliteConnection db = new SqliteConnection("Data Source=" + databaseLocalFile.Path))
			{
				db.Open();
				SqliteCommand sqliteCommand = new SqliteCommand("SELECT * FROM package_tiles WHERE id=1", db);
				var reader = sqliteCommand.ExecuteReader();
				;
				while (reader.Read())
				{
					var data = reader["tile_data"] as byte[];
					ParseData(data);

					System.Diagnostics.Debug.WriteLine("====");
					for(int i=0; i<reader.FieldCount; i++)
						System.Diagnostics.Debug.WriteLine($"{reader.GetFieldType(i)} => {reader.GetFieldValue<string>(i)}");
					;
				}

					//String tableCommand = "CREATE TABLE IF NOT " +
					//	"EXISTS MyTable (Primary_Key INTEGER PRIMARY KEY, " +
					//	"Text_Entry NVARCHAR(2048) NULL)";

					//SqliteCommand createTable = new SqliteCommand(tableCommand, db);

					//createTable.ExecuteReader();
				}
			return "";
		}
	}
}
