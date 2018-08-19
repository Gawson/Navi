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

using Windows.Media.Capture;
using Windows.ApplicationModel;
using System.Threading.Tasks;
using Windows.System.Display;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.ApplicationModel.Core;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace HDMICapture
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
		MediaCapture mediaCapture;
		bool isPreviewing;
		DisplayRequest displayRequest = new DisplayRequest();

		public MainPage()
        {
            this.InitializeComponent();

			Application.Current.Suspending += Application_Suspending;
		}

		private async Task StartPreviewAsync()
		{
			try
			{

				mediaCapture = new MediaCapture();				
				await mediaCapture.InitializeAsync();
				

				displayRequest.RequestActive();
				DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
			}
			catch (UnauthorizedAccessException)
			{
				// This will be thrown if the user denied access to the camera in privacy settings
				//ShowMessageToUser("The app was denied access to the camera");
				return;
			}

			try
			{
				PreviewControl.Source = mediaCapture;
				PreviewControl.UseLayoutRounding = true;
				await mediaCapture.StartPreviewAsync();
				isPreviewing = true;
			}
			catch (System.IO.FileLoadException)
			{
				mediaCapture.CaptureDeviceExclusiveControlStatusChanged += _mediaCapture_CaptureDeviceExclusiveControlStatusChanged;
			}

		}

		private async void _mediaCapture_CaptureDeviceExclusiveControlStatusChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
		{
			if (args.Status == MediaCaptureDeviceExclusiveControlStatus.SharedReadOnlyAvailable)
			{
				//ShowMessageToUser("The camera preview can't be displayed because another app has exclusive access");
			}
			else if (args.Status == MediaCaptureDeviceExclusiveControlStatus.ExclusiveControlAvailable && !isPreviewing)
			{
				await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
				{
					await StartPreviewAsync();
				});
			}
		}

		private async Task CleanupCameraAsync()
		{
			if (mediaCapture != null)
			{
				if (isPreviewing)
				{
					await mediaCapture.StopPreviewAsync();
				}

				await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
				{
					PreviewControl.Source = null;
					if (displayRequest != null)
					{
						displayRequest.RequestRelease();
					}

					mediaCapture.Dispose();
					mediaCapture = null;
				});
			}

		}

		protected async override void OnNavigatedFrom(NavigationEventArgs e)
		{
			await CleanupCameraAsync();
		}


		private async void Application_Suspending(object sender, SuspendingEventArgs e)
		{
			// Handle global application events only if this page is active
			if (Frame.CurrentSourcePageType == typeof(MainPage))
			{
				var deferral = e.SuspendingOperation.GetDeferral();
				await CleanupCameraAsync();
				deferral.Complete();
			}
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			StartPreviewAsync();
		}

		private async void Button_Click_1(object sender, RoutedEventArgs e)
		{
			await CleanupCameraAsync();
			CoreApplication.Exit();
		}
	}
}
