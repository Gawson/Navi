using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.Devices.Geolocation;
using Windows.UI.Core;
using GpsPreview.Maps;
using System.Text;
using Windows.UI.Xaml.Shapes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas;
using Windows.UI;
using Windows.Storage;
using Microsoft.Graphics.Canvas.Svg;
using Itinero;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace GpsPreview
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
		MapTilesDatabase db;
		Geoposition pos;
		Geolocator geolocator;

		Tile currentTile = null;

		private float angle = 0;
		private float tileScale = 1;
		private float tileScaleFactor = 0.1f / 120f;
		private bool panWithPointer = false;
		private System.Numerics.Vector2 canvasOffset = new System.Numerics.Vector2(0, 0);
		private System.Numerics.Vector2 canvasScalePoint = new System.Numerics.Vector2(0, 0);
		private Windows.UI.Input.PointerPoint lastPoint = null;

		Dictionary<string, CanvasSvgDocument> icons;

		public MainPage()
        {
            this.InitializeComponent();

			this.Loaded += MainPage_Loaded;
        }

		RouterDb routerDb;
		Router router;
		bool setStartPending = false;
		bool setEndPending = false;
		BasicGeoposition startPosition;
		BasicGeoposition endPosition;
		Windows.UI.Xaml.Controls.Maps.MapIcon startIcon;
		Windows.UI.Xaml.Controls.Maps.MapIcon endIcon;

		private async void InitMapRouter()
		{
			//=================================================
			Windows.Storage.StorageFolder storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
			Windows.Storage.StorageFile routerFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Maps//poland.routerdb"));
			routerDb = Itinero.RouterDb.Deserialize(new FileInfo(routerFile.Path).OpenRead(), Itinero.RouterDbProfile.MobileHighEnd);
			router = new Itinero.Router(routerDb);
			
			//=================================================
		}

		private async void MainPage_Loaded(object sender, RoutedEventArgs e)
		{
			db = new MapTilesDatabase();
			db.InitFromResource(MapTilesDatabase.PolandCustomMapBase);

			//MAP 
			map.MapTapped += Map_MapTapped;
			map.Center = new Geopoint(new BasicGeoposition() { Longitude = 21.006114275336859, Latitude = 52.231777083350494, Altitude = 163.6815999513492 });
			map.ZoomLevel = 16;

			InitMapRouter();


			Console.SetOut(new ControlWriter(textOutput));

			win2dCanvas.PointerWheelChanged += (s, args) =>
			{
				var point = args.GetCurrentPoint(win2dCanvas);
				//System.Diagnostics.Debug.WriteLine($"{point.Properties.MouseWheelDelta}");
				tileScale += tileScale*(point.Properties.MouseWheelDelta * tileScaleFactor);
				tileScale = Math.Max(0, Math.Min(10, tileScale));
				canvasScalePoint = new System.Numerics.Vector2((float)point.Position.X, (float)point.Position.Y);
				win2dCanvas.Invalidate();
			};

			win2dCanvas.PointerPressed += (s, args) =>
			{
				panWithPointer = true;
				if(args.GetCurrentPoint(null).Properties.IsRightButtonPressed)
				{
					canvasOffset = new System.Numerics.Vector2(0, 0);
					tileScale = 1f;
					win2dCanvas.Invalidate();
				}
			};

			win2dCanvas.PointerMoved += (s, args) =>
			{
				if(panWithPointer)
				{
					var cp = args.GetCurrentPoint(null);
					if (lastPoint == null)
						lastPoint = cp;
					canvasOffset = new System.Numerics.Vector2(canvasOffset.X + (float)(cp.Position.X - lastPoint.Position.X), canvasOffset.Y + (float)(cp.Position.Y - lastPoint.Position.Y));
					lastPoint = cp;
					win2dCanvas.Invalidate();
				}
			};

			win2dCanvas.PointerReleased += (s, args) =>
			{
				panWithPointer = false;
				lastPoint = null;
			};

			win2dCanvas.DpiScale = 1;

			icons = new Dictionary<string, CanvasSvgDocument>();
			StorageFolder appInstalledFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
			StorageFolder assets = await appInstalledFolder.GetFolderAsync("Icons");
			var files = await assets.GetFilesAsync();
			foreach (var f in files) {
				var shortName = System.IO.Path.GetFileNameWithoutExtension(f.Name).Split(".").Last();
				using (var stream = await f.OpenAsync(FileAccessMode.Read)) {
					CanvasSvgDocument svg = await CanvasSvgDocument.LoadAsync(win2dCanvas, stream);
					icons.Add(shortName, svg);
				}
			}
			;
		}

		

		private async void Map_MapTapped(Windows.UI.Xaml.Controls.Maps.MapControl sender, Windows.UI.Xaml.Controls.Maps.MapInputEventArgs args)
		{
			if(setStartPending)
			{
				// get position
				Geopoint myPoint = new Geopoint(new BasicGeoposition() { Latitude = args.Location.Position.Latitude, Longitude = args.Location.Position.Longitude });
				//create POI
				startIcon = new Windows.UI.Xaml.Controls.Maps.MapIcon { Location = myPoint, NormalizedAnchorPoint = new Point(0.5, 1.0), Title = "Start", ZIndex = 0 };
				map.MapElements.Add(startIcon);
				setStartPending = false;
				return;
			}

			if(setEndPending)
			{
				// get position
				Geopoint myPoint = new Geopoint(new BasicGeoposition() { Latitude = args.Location.Position.Latitude, Longitude = args.Location.Position.Longitude });
				//create POI
				endIcon = new Windows.UI.Xaml.Controls.Maps.MapIcon { Location = myPoint, NormalizedAnchorPoint = new Point(0.5, 1.0), Title = "End", ZIndex = 0 };
				map.MapElements.Add(endIcon);
				setEndPending = false;
				return;
			}


			int zoomLevel = Math.Min(14, Math.Max(1, (int)sender.ZoomLevel));
			var tileAddr = MapUtil.WorldToTilePos(args.Location.Position.Longitude, args.Location.Position.Latitude, zoomLevel);
			System.Diagnostics.Debug.WriteLine($"{args.Location.Position.Latitude}, {args.Location.Position.Longitude}");
			Map_MapTapped((int)tileAddr.X, (int)tileAddr.Y, zoomLevel);
		}

		private async void Map_MapTapped(int tileX, int tileY, int zoomLevel)
		{
			float canvasScale = 1f;
			float canvasSize = 768;
			float lineScale = 1f;

			//int zoomLevel = Math.Min(14, Math.Max(1, (int)sender.ZoomLevel));
			//var tileAddr = MapUtil.WorldToTilePos(args.Location.Position.Longitude, args.Location.Position.Latitude, zoomLevel);
			System.Diagnostics.Debug.WriteLine($"{tileX} x {tileY}");
			System.Diagnostics.Debug.WriteLine($"zoom: {zoomLevel}");
			{
				var reader = await db.GetTile((int)tileX, (int)tileY, zoomLevel);
				if (!reader.HasRows)
					textOutput.Text = DateTime.Now + ": No data";
				var tile = VectorTile.Parse(reader);
				if (tile.tile_data_raw != null)
				{
					PBF pbf = new PBF(tile.tile_data_raw);
					currentTile = ProtoBuf.Serializer.Deserialize<Tile>(new MemoryStream(tile.tile_data_raw));

					if (GeometryDecoder.CanvasTileId != tile.id)
					{
						GeometryDecoder.CanvasTileId = tile.id;

						if (GeometryDecoder.offscreen == null)
						{
							CanvasDevice device = CanvasDevice.GetSharedDevice();
							GeometryDecoder.offscreen = new CanvasRenderTarget(device, canvasSize, canvasSize, Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi * canvasScale);
							//GeometryDecoder.offscreenText = new CanvasRenderTarget(device, canvasSize, canvasSize, Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi * canvasScale);
						}


						//Performance benchmark
						Tile.Layer layer_buildings = null;
						Tile.Layer layer_landcover = null;
						Tile.Layer layer_transportation = null;
						Tile.Layer layer_transportation_name = null;
						Tile.Layer layer_housenumber = null;
						Tile.Layer layer_poi = null;

						if (currentTile.Layers.Any(l => l.Name == "building"))
							layer_buildings = currentTile.Layers.Where(l => l.Name == "building").ToList()?.First();
						if (currentTile.Layers.Any(l => l.Name == "landcover"))
							layer_landcover = currentTile.Layers.Where(l => l.Name == "landcover").ToList()?.First();
						if (currentTile.Layers.Any(l => l.Name == "transportation"))
							layer_transportation = currentTile.Layers.Where(l => l.Name == "transportation").ToList()?.First();
						if (currentTile.Layers.Any(l => l.Name == "transportation_name"))
							layer_transportation_name = currentTile.Layers.Where(l => l.Name == "transportation_name").ToList()?.First();
						if (currentTile.Layers.Any(l => l.Name == "housenumber"))
							layer_housenumber = currentTile.Layers.Where(l => l.Name == "housenumber").ToList()?.First();
						if (currentTile.Layers.Any(l => l.Name == "poi"))
							layer_poi = currentTile.Layers.Where(l => l.Name == "poi").ToList()?.First();

						System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
						stopwatch.Start();

						Dictionary<string, Windows.UI.Color> landColors = new Dictionary<string, Windows.UI.Color>();
						landColors.Add("grass", Windows.UI.Colors.LawnGreen);
						landColors.Add("meadow", Windows.UI.Colors.Green);
						landColors.Add("wood", Windows.UI.Colors.ForestGreen);
						landColors.Add("forest", Windows.UI.Colors.DarkGreen);
						landColors.Add("park", Windows.UI.Colors.LightGreen);
						landColors.Add("village_green", Windows.UI.Colors.GreenYellow);
						landColors.Add("wetland", Windows.UI.Colors.CornflowerBlue);
						landColors.Add("recreation_ground", Windows.UI.Colors.LightYellow);
						landColors.Add("allotments", Windows.UI.Colors.Red);

						Dictionary<string, Tuple<Color, float, Color, float>> roadProperties = new Dictionary<string, Tuple<Color, float, Color, float>>();
						roadProperties.Add("transit", new Tuple<Color, float, Color, float>(Colors.Black, 0.5f, Colors.Black, 0.5f));
						roadProperties.Add("primary", new Tuple<Color, float, Color, float>(Colors.LightYellow, 3, Colors.SandyBrown, 4.5f));
						roadProperties.Add("secondary", new Tuple<Color, float, Color, float>(Colors.LightYellow, 2, Colors.SandyBrown, 3f));
						roadProperties.Add("tertiary", new Tuple<Color, float, Color, float>(Colors.LightYellow, 2, Colors.SandyBrown, 3f));
						roadProperties.Add("minor", new Tuple<Color, float, Color, float>(Colors.WhiteSmoke, 1.8f, Colors.Gray, 2.5f));
						roadProperties.Add("service", new Tuple<Color, float, Color, float>(Colors.WhiteSmoke, 1.8f, Colors.Gray, 2.5f));
						roadProperties.Add("track", new Tuple<Color, float, Color, float>(Colors.LightGray, 1, Colors.Gray, 2));
						roadProperties.Add("path", new Tuple<Color, float, Color, float>(Colors.LightGray, 1, Colors.Gray, 2));
						roadProperties.Add("rail", new Tuple<Color, float, Color, float>(Colors.Gainsboro, 0.75f, Colors.DimGray, 1.4f));
						roadProperties.Add("motorway", new Tuple<Color, float, Color, float>(Colors.Orange, 3, Colors.Red, 4.5f));
						roadProperties.Add("trunk", new Tuple<Color, float, Color, float>(Colors.Orange, 3, Colors.Red, 4.5f));


						GeometryDecoder.renderList = new CanvasCommandList(GeometryDecoder.offscreen);
						GeometryDecoder.renderText = new CanvasCommandList(GeometryDecoder.offscreen);

						//using (CanvasDrawingSession ds = GeometryDecoder.offscreen.CreateDrawingSession())
						using (CanvasDrawingSession textDs = GeometryDecoder.renderText.CreateDrawingSession())
						using (CanvasDrawingSession ds = GeometryDecoder.renderList.CreateDrawingSession())
						using (CanvasActiveLayer activeLayer = ds.CreateLayer(1, new Rect(0, 0, canvasSize, canvasSize)))
						using (CanvasActiveLayer activeTextLayer = textDs.CreateLayer(1, new Rect(0, 0, canvasSize, canvasSize)))
						{

							ds.Antialiasing = CanvasAntialiasing.Antialiased;
							ds.Clear(Windows.UI.Colors.Snow);
							for (int it = 0; it < 1; it++)
							{
								layer_landcover?.Features.ForEach(f =>
								{
									var tags = GeometryDecoder.GetTags(f, layer_landcover);
									var color = Colors.LightGreen;
									if (tags.ContainsKey("subclass"))
										color = landColors.ContainsKey(tags["subclass"].StringValue) ? landColors[tags["subclass"].StringValue] : Windows.UI.Colors.LightGreen;
									GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, color, color);
								});

								layer_buildings?.Features.ForEach(f =>
								{
									var tags = GeometryDecoder.GetTags(f, layer_buildings);
									GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, Windows.UI.Colors.SandyBrown, Windows.UI.Colors.Brown);
								});


								List<string> tagsNames = new List<string>();
								var names = layer_transportation?.Features.ConvertAll<Tuple<string, string>>(f =>
								 {
									 var tags = GeometryDecoder.GetTags(f, layer_transportation);

									 foreach (var t in tags)
										 tagsNames.Add(t.Key);
									 return new Tuple<string, string>(tags.ContainsKey("class") ? tags["class"].StringValue : "", tags.ContainsKey("subclass") ? tags["subclass"].StringValue : "");
								 }).ToList().Distinct().ToList();

								var distinctTagsNames = tagsNames.Distinct().ToList();

								Tuple<Color, float, Color, float> rc = new Tuple<Color, float, Color, float>(Colors.Pink, 2, Colors.DeepPink, 2);


								Dictionary<int, List<Tile.Feature>> transportationFeatures = new Dictionary<int, List<Tile.Feature>>();
								foreach (var f in layer_transportation.Features)
								{
									var tags = GeometryDecoder.GetTags(f, layer_transportation);
									int layer = tags.ContainsKey("layer") ? (int)tags["layer"].IntValue : 0;
									if (!transportationFeatures.ContainsKey(layer))
										transportationFeatures.Add(layer, new List<Tile.Feature>());
									transportationFeatures[layer].Add(f);
								}

								foreach (int layerNo in transportationFeatures.Keys.OrderBy(k => k))
								{
									//background / stroke pass
									foreach (var f in transportationFeatures[layerNo])
									{
										var tags = GeometryDecoder.GetTags(f, layer_transportation);

										if (tags.ContainsKey("class"))
										{
											if (roadProperties.ContainsKey(tags["class"].StringValue))
											{
												var p = roadProperties[tags["class"].StringValue];
												GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, p.Item1, p.Item3, 0, p.Item4 * lineScale, layerNo > 0 ? GeometryDecoder.openStrokeStyle : GeometryDecoder.normalStrokeStyle);
											}
											else
											{
												GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, rc.Item1, rc.Item3, 0, rc.Item4 * lineScale, GeometryDecoder.normalStrokeStyle);
											}
										}
										else
											GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, rc.Item1, rc.Item3, 0, rc.Item4 * lineScale, GeometryDecoder.normalStrokeStyle);
									}

									//foreground / fill pass
									foreach (var f in transportationFeatures[layerNo])
									{
										var tags = GeometryDecoder.GetTags(f, layer_transportation);
										if (tags.ContainsKey("class"))
										{
											if (roadProperties.ContainsKey(tags["class"].StringValue))
											{
												var p = roadProperties[tags["class"].StringValue];
												if (tags["class"].StringValue == "rail" || tags["class"].StringValue == "transit")
												{
													GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, p.Item1, p.Item3, p.Item2 * lineScale, 0, GeometryDecoder.railStrokeStyle);
													//												System.Diagnostics.Debug.WriteLine(t)
												}
												else
													GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, p.Item1, p.Item3, p.Item2 * lineScale, 0, GeometryDecoder.normalStrokeStyle);
											}
											else
											{
												System.Diagnostics.Debug.WriteLine($"Unsupported class: {tags["class"]}");
												GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, rc.Item1, rc.Item3, rc.Item2 * lineScale, 0, GeometryDecoder.normalStrokeStyle);
											}
										}
										else
											GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, rc.Item1, rc.Item3, rc.Item2 * lineScale, 0, GeometryDecoder.normalStrokeStyle);
									}
								}



								//								layer_transportation?.Features.ForEach(f =>
								//								{
								//									var tags = GeometryDecoder.GetTags(f, layer_transportation);

								//									if (tags.ContainsKey("class"))
								//									{
								//										if (roadProperties.ContainsKey(tags["class"].StringValue))
								//										{
								//											var p = roadProperties[tags["class"].StringValue];
								//											GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, p.Item1, p.Item3, 0, p.Item4 * lineScale);
								//										}
								//										else
								//										{
								//											GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, rc.Item1, rc.Item3, 0, rc.Item4 * lineScale);
								//										}
								//									}
								//									else
								//										GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, rc.Item1, rc.Item3, 0, rc.Item4 * lineScale);
								//								});

								//								layer_transportation?.Features.ForEach(f =>
								//								{
								//									var tags = GeometryDecoder.GetTags(f, layer_transportation);
								//									if (tags.ContainsKey("class"))
								//									{
								//										if (roadProperties.ContainsKey(tags["class"].StringValue))
								//										{
								//											var p = roadProperties[tags["class"].StringValue];
								//											if (tags["class"].StringValue == "rail")
								//											{
								//												GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, p.Item1, p.Item3, p.Item2 * lineScale, 0, GeometryDecoder.railStrokeStyle);
								////												System.Diagnostics.Debug.WriteLine(t)
								//											}
								//											else
								//												GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, p.Item1, p.Item3, p.Item2 * lineScale, 0);
								//										}
								//										else
								//										{
								//											System.Diagnostics.Debug.WriteLine($"Unsupported class: {tags["class"]}");
								//											GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, rc.Item1, rc.Item3, rc.Item2 * lineScale, 0);
								//										}
								//									}
								//									else
								//										GeometryDecoder.DrawGeometry(f, canvasSize / 4096f, ds, rc.Item1, rc.Item3, rc.Item2 * lineScale, 0);
								//								});

								

								layer_housenumber?.Features.ForEach(f =>
								{
									var tags = GeometryDecoder.GetTags(f, layer_housenumber);
									string text = tags["housenumber"].StringValue;
									//GeometryDecoder.DrawHousenumber(f, canvasSize / 4096f, textDs, Colors.Black, Colors.White, 1, 1.2f, text);
									GeometryDecoder.DrawText(f, canvasSize / 4096f, textDs, Colors.Black, Colors.White, 1, 1.2f, text, 2f);
								});

								layer_poi?.Features.ForEach(f => {
									var tags = GeometryDecoder.GetTags(f, layer_poi);
									if(tags.ContainsKey("subclass"))
									{
										string subclass = tags["subclass"].StringValue;
										if(icons.ContainsKey(subclass+"_11"))
										{
											GeometryDecoder.DrawIcon(f, canvasSize / 4096f, ds, icons[subclass + "_11"], Colors.Black, Colors.DarkGray);
										}
									}
								});


								layer_transportation_name?.Features.ForEach(f =>
								{
									var tags = GeometryDecoder.GetTags(f, layer_transportation_name);
									if (tags.ContainsKey("name"))
									{
										string text = tags["name"].StringValue;
										GeometryDecoder.DrawText(f, canvasSize / 4096f, textDs, Colors.Black, Colors.White, 1, 1.2f, text, 3f);
									}
								});
							}
						}
						//StorageFolder storageFolder = KnownFolders.PicturesLibrary;
						//StorageFile file = await storageFolder.CreateFileAsync("map.bmp", CreationCollisionOption.ReplaceExisting);
						//var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
						//await GeometryDecoder.offscreen.SaveAsync(stream, CanvasBitmapFileFormat.Bmp);
						//stream.Dispose();

						

						stopwatch.Stop();


						System.Diagnostics.Debug.WriteLine($"TIME: {stopwatch.ElapsedMilliseconds} ms");
						textOutput.Text = $"TIME: {stopwatch.ElapsedMilliseconds} ms";
					}
				}
				else
				{
					currentTile = null;
				}

			}

			win2dCanvas.Invalidate();
		}


		private async void gpsButton_Click(object sender, RoutedEventArgs e)
		{
			var accessStatus = await Geolocator.RequestAccessAsync();

			switch (accessStatus)
			{
				case GeolocationAccessStatus.Allowed:
					textOutput.Text = "Waiting for update...";

					// If DesiredAccuracy or DesiredAccuracyInMeters are not set (or value is 0), DesiredAccuracy.Default is used.
					geolocator = new Geolocator { DesiredAccuracyInMeters = 5 };

					// Subscribe to the StatusChanged event to get updates of location status changes.
					geolocator.StatusChanged += OnStatusChanged;

					// Carry out the operation.
					pos = await geolocator.GetGeopositionAsync();

					//TODO
					//UpdateLocationData(pos);
					

					textOutput.Text = "Location updated.";
					
					break;

				case GeolocationAccessStatus.Denied:
					textOutput.Text = "Access to location is denied.";
					//LocationDisabledMessage.Visibility = Visibility.Visible;
					//UpdateLocationData(null);
					break;

				case GeolocationAccessStatus.Unspecified:
					textOutput.Text = "Unspecified error.";
					//UpdateLocationData(null);
					break;
			}
		}


		async private void OnStatusChanged(Geolocator sender, StatusChangedEventArgs e)
		{
			await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				// Show the location setting message only if status is disabled.
				//LocationDisabledMessage.Visibility = Visibility.Collapsed;

				switch (e.Status)
				{
					case PositionStatus.Ready:
						// Location platform is providing valid data.
						textOutput.Text = "Ready";
						textOutput.Text += "\n";
						textOutput.Text += "Location platform is ready.";
						break;

					case PositionStatus.Initializing:
						// Location platform is attempting to acquire a fix.
						textOutput.Text = "Initializing";
						textOutput.Text += "\n";
						textOutput.Text += "Location platform is attempting to obtain a position.";
						break;

					case PositionStatus.NoData:
						// Location platform could not obtain location data.
						textOutput.Text = "No data";
						textOutput.Text += "\n";
						textOutput.Text += "Not able to determine the location.";
						break;

					case PositionStatus.Disabled:
						// The permission to access location data is denied by the user or other policies.
						textOutput.Text = "Disabled";
						textOutput.Text += "\n";
						textOutput.Text += "Access to location is denied.";

						// Show message to the user to go to location settings.
						//LocationDisabledMessage.Visibility = Visibility.Visible;

						// Clear any cached location data.
						//UpdateLocationData(null);
						break;

					case PositionStatus.NotInitialized:
						// The location platform is not initialized. This indicates that the application
						// has not made a request for location data.
						textOutput.Text = "Not initialized";
						textOutput.Text += "\n";
						textOutput.Text += "No request for location is made yet.";
						break;

					case PositionStatus.NotAvailable:
						// The location platform is not available on this version of the OS.
						textOutput.Text = "Not available";
						textOutput.Text += "\n";
						textOutput.Text += "Location is not available on this version of the OS.";
						break;

					default:
						textOutput.Text = "Unknown";
						break;
				}
			});
		}

		private async void updateButton_Click(object sender, RoutedEventArgs e)
		{
			var result = await geolocator.GetGeopositionAsync();
			map.Center = result.Coordinate.Point;			
		}

		private async void sqliteTest_Click(object sender, RoutedEventArgs e)
		{
			//var output = await MapTilesDatabase.InitializeDatabase();
			//textOutput.Text += "\n" + output + "\n";
		}

		
		private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
		{

			if(currentTile==null)
			{
				args.DrawingSession.Clear(Windows.UI.Colors.CornflowerBlue);
			} else
			{
				if (GeometryDecoder.renderList != null)
				{
					System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
					stopwatch.Start();
					//var size = sender.Size;
					//args.DrawingSession.Transform = System.Numerics.Matrix3x2.CreateRotation(angle, new System.Numerics.Vector2((float)size.Width/2f, (float)size.Height/2f));
					args.DrawingSession.Transform = System.Numerics.Matrix3x2.CreateScale(tileScale, canvasScalePoint) * System.Numerics.Matrix3x2.CreateTranslation(canvasOffset);
					args.DrawingSession.DrawImage(GeometryDecoder.renderList);
					GeometryDecoder.ApplyTextLayer(args.DrawingSession);
					args.DrawingSession.DrawImage(GeometryDecoder.renderText);
					stopwatch.Stop();
					System.Diagnostics.Debug.WriteLine($"Render time: {stopwatch.ElapsedMilliseconds} ms");
				}
			}
		}

		private void win2dAnimatedCanvas_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
		{
			if (currentTile == null)
			{
				args.DrawingSession.Clear(Windows.UI.Colors.CornflowerBlue);
			}
			else
			{
				if (GeometryDecoder.renderList != null)
				{
					//System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
					//stopwatch.Start();
					var size = sender.Size;
					args.DrawingSession.Transform = System.Numerics.Matrix3x2.CreateRotation(angle, new System.Numerics.Vector2((float)size.Width / 2f, (float)size.Height / 2f));
					args.DrawingSession.DrawImage(GeometryDecoder.renderList, new Rect(0,0,1024,1024), new Rect(0, 0, 1024, 1024));
					//stopwatch.Stop();
					//System.Diagnostics.Debug.WriteLine($"Render time: {stopwatch.ElapsedMilliseconds} ms");
				}
			}
		}

		private void loadTile_Click(object sender, RoutedEventArgs e)
		{			
			var tileAddr = MapUtil.WorldToTilePos(21.006079, 52.231748, (int)zoomLevelSlider.Value);
			Map_MapTapped((int)tileAddr.X, (int)tileAddr.Y, (int)zoomLevelSlider.Value);
		}

		private void clearRouter_Click(object sender, RoutedEventArgs e)
		{
			map.MapElements.Clear();
		}

		private void setStartPoint_Click(object sender, RoutedEventArgs e)
		{
			setStartPending = true;
		}

		private void setEndPoint_Click(object sender, RoutedEventArgs e)
		{
			setEndPending = true;
		}

		private void searchRoute_Click(object sender, RoutedEventArgs e)
		{
			


			if (startIcon == null || endIcon == null) return;

			var cycleWays = routerDb.HasContractedFor(Itinero.Osm.Vehicles.Vehicle.Bicycle.Fastest());
			var cycleProfiles = new List<Itinero.Profiles.Profile>(Itinero.Osm.Vehicles.Vehicle.Bicycle.GetProfiles());
			var profile = cycleProfiles[0];
			var edgeFunc = new Func<Itinero.Data.Network.RoutingEdge, bool>((edge) => {
				Console.WriteLine(edge.ToString() + " " + edge.Data.Distance);
				if (edge.Data.Distance > 40) return true;
				return false;
			});
			//var start = router.Resolve(new Itinero.Profiles.Profile[] { profile }, (float)startIcon.Location.Position.Latitude, (float)startIcon.Location.Position.Longitude, edgeFunc);
			var start = router.Resolve(profile, (float)startIcon.Location.Position.Latitude, (float)startIcon.Location.Position.Longitude,100);
			var end = router.Resolve(profile, (float)endIcon.Location.Position.Latitude, (float)endIcon.Location.Position.Longitude,100);
			try
			{
				var route = router.Calculate(profile, start, end);
				var routeGeoJson = route.ToGeoJson();
				//Console.WriteLine(routeGeoJson);
				Console.WriteLine($"Time: {route.TotalTime / 60f}");

				foreach (var c in route.Shape)
				{
					Console.WriteLine($"new BasicGeoposition() {{ Latitude = {c.Latitude}f, Longitude = {c.Longitude}f }},");
				}


				var mPoly = new Windows.UI.Xaml.Controls.Maps.MapPolyline();
				var positions = route.Shape.ToList().ConvertAll<BasicGeoposition>(s => new BasicGeoposition() { Latitude = s.Latitude, Longitude = s.Longitude });
				mPoly.Path = new Geopath(positions);
				map.MapElements.Clear();
				map.MapElements.Add(startIcon);
				map.MapElements.Add(endIcon);
				map.MapElements.Add(mPoly);
			}
			catch (Exception)
			{
				Console.WriteLine("No route found");
			}
		}
	}
}
