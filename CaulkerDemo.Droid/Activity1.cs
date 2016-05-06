using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Caulker;
using System.Drawing;
using Android.Content.PM;

namespace CaulkerDemo.Droid
{
    [Activity(Label = "CaulkerDemo.Droid", MainLauncher = true, Icon = "@drawable/icon",
              ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden, LaunchMode = LaunchMode.SingleTask)]
    public class Activity1 : Activity
    {
        WorldView _worldView;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            HookCrashLogger();

            //
            // Create the view
            //
            _worldView = new WorldView(this, Resource.Drawable.MissingTile)
            {
                ShowSun = false
            };

            //
            // Add a layer of tiles
            //
            var tiles = new TileRenderer();
            var sourceIndex = 0;
            tiles.Source = _tileSources[sourceIndex];
            _worldView.AddDrawable(tiles);

            //
            // Add a button to toggle tile sources
            //
            var toggle = new Button(this);
            toggle.Text = _tileSources[0].Name;
            toggle.Click += delegate
            {
                sourceIndex = (sourceIndex + 1) % _tileSources.Length;
                toggle.Text = _tileSources[sourceIndex].Name;
                tiles.Source = _tileSources[sourceIndex];
            };

            var root = new LinearLayout(this);
            root.LayoutParameters = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.FillParent,
                LinearLayout.LayoutParams.FillParent);
            root.Orientation = Orientation.Vertical;

            root.AddView(_worldView);
            root.AddView(toggle);

            this.SetContentView(root);
            _worldView.Run(4);
        }

        private void HookCrashLogger()
        {
            //Trying to catch exception through Mono environment
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine("Exception caught through Mono");

                WriteException(e.ExceptionObject as Exception);
            };

            //Added based on http://www.mail-archive.com/monodroid@lists.ximian.com/msg03654.html
            AndroidEnvironment.UnhandledExceptionRaiser += (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine("Exception caught through AndroidEnvironment");

                WriteException(e.Exception);
            };
        }

        private void WriteException(Exception ex)
        {
            if (ex != null)
                Console.WriteLine(ex.Message + "\n" + ex.StackTrace);
        }

        protected override void OnPause()
        {
            base.OnPause();
            _worldView.Pause();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _worldView.Resume();
        }

        TileSource[] _tileSources = new TileSource[] {
			new OpenStreetMapTileSource(),
			new GoogleTileSource(),
			new BingTileSource(),
			new CycleMapTileSource(),
			new GoogleTerrainTileSource(),
			new GoogleSatelliteTileSource(),
			new BingSatelliteTileSource(),
			new GoogleMoonTileSource(),
		};
    }
}

