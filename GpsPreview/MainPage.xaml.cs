﻿using System;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace GpsPreview
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

		Geoposition pos;
		Geolocator geolocator;

		public MainPage()
        {
            this.InitializeComponent();
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
			var output = await MapTilesDatabase.InitializeDatabase();
			textOutput.Text += "\n" + output + "\n";
		}
	}
}
