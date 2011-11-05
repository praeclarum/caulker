
using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using Caulker;
using System.Drawing;

namespace CaulkerDemo
{
	public class Application
	{
		static void Main (string[] args)
		{
			UIApplication.Main (args);
		}
	}

	public partial class AppDelegate : UIApplicationDelegate
	{
		WorldView _worldView;
		
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			//
			// Create the view
			//
			_worldView = new WorldView (window.Bounds) {
				ShowSun = true,
			};
			window.AddSubview(_worldView);
			window.SendSubviewToBack(_worldView);
			_worldView.Run(20);
			
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
			var toggle = new UIButton(new RectangleF(0,450,120,30));
			toggle.Font = UIFont.BoldSystemFontOfSize(14);
			toggle.SetTitle(_tileSources[0].Name, UIControlState.Normal);
			toggle.SetTitleColor(UIColor.Black, UIControlState.Normal);
			toggle.TouchUpInside += delegate {
				sourceIndex = (sourceIndex + 1) % _tileSources.Length;
				toggle.SetTitle(_tileSources[sourceIndex].Name, UIControlState.Normal);
				tiles.Source = _tileSources[sourceIndex];
			};
			window.AddSubview(toggle);
			
			window.MakeKeyAndVisible ();
			
			return true;
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

		public override void OnActivated (UIApplication application)
		{
		}
	}
}

