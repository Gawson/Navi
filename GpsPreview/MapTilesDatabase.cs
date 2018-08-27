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
		public static string WarsawMapBase = "Maps//2017-07-03_poland_warsaw.mbtiles";
		public static string PolandMapBase = "Maps//2017-07-03_europe_poland.mbtiles";

		string mapDbFile;
		SqliteConnection db;		

		

		public async Task InitFromResource(string resourceFile)
		{
			Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
			Windows.Storage.StorageFile databaseLocalFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///" + resourceFile));

			await InitializeDatabase(databaseLocalFile.Path);
		}
		
		public async Task InitializeDatabase(string databaseFilePath)
		{
			try
			{
				db = new SqliteConnection("Data Source=" + databaseFilePath);
				db.Open();
			} catch(Exception exc)
			{
				System.Diagnostics.Debug.WriteLine("[EXCEPTION] " + exc.Message);
				;
			}
		}

		async Task<SqliteDataReader> RunCommand(string command)
		{
			SqliteCommand sqliteCommand = new SqliteCommand(command, db);
			return await sqliteCommand.ExecuteReaderAsync();
		}

		public async Task<SqliteDataReader> GetTile(int tile_x, int tile_y, int zoom)
		{
			return await RunCommand($"SELECT * FROM package_tiles WHERE tile_column = {tile_x} and tile_row = {tile_y}");
		}

	}
	
}
