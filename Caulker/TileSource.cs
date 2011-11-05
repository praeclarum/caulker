//
// Copyright (c) 2010 Krueger Systems, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace Caulker {
	
	public abstract class TileSource {
		public string Name { get; protected set; }
		public string FileExtension { get; protected set; }
		public bool FlipVertical { get; protected set; }
		
		public abstract string GetTileUrl(TileName name);
	}
	
	public class OpenStreetMapTileSource : TileSource {
		public OpenStreetMapTileSource() {
			Name = "OpenStreetMap";
			FileExtension = ".png";
		}
		public override string GetTileUrl(TileName name) {
			return string.Format("http://tile.openstreetmap.org/{0}/{1}/{2}.png",
			                     name.Zoom,
			                     name.X, name.Y);
		}
	}

	public class TilesAtHomeTileSource : TileSource {
		public TilesAtHomeTileSource() {
			Name = "TilesAtHome";
			FileExtension = ".png";
		}
		public override string GetTileUrl(TileName name) {
			return string.Format("http://tah.openstreetmap.org/Tiles/tile/{0}/{1}/{2}.png",
			                     name.Zoom,
			                     name.X, name.Y);
		}
	}
	
	public class GoogleTileSource : TileSource {
		public GoogleTileSource() {
			Name = "Google";
			FileExtension = ".png";
		}
		public override string GetTileUrl(TileName name) {
			return string.Format("http://mt1.google.com/vt/lyrs=m@126&hl=en&x={1}&s=&y={2}&z={0}&s=Gali",
			                     name.Zoom,
			                     name.X, name.Y);
		}
	}

	public class GoogleMoonTileSource : TileSource {
		public GoogleMoonTileSource() {
			Name = "GoogleMoon";
			FileExtension = ".png";
			FlipVertical = true;
		}
		public override string GetTileUrl(TileName name) {
			return string.Format("http://mw1.google.com/mw-planetary/lunar/lunarmaps_v1/clem_bw/{0}/{1}/{2}.jpg",
			                     name.Zoom,
			                     name.X, name.Y);
		}
	}
	
	public class GoogleTerrainTileSource : TileSource {
		public GoogleTerrainTileSource() {
			Name = "GoogleTerrain";
			FileExtension = ".png";
		}
		public override string GetTileUrl(TileName name) {
			return string.Format("http://mt0.google.com/vt/lyrs=t@125,r@126&hl=en&x={1}&s=&y={2}&z={0}&s=",
			                     name.Zoom,
			                     name.X, name.Y);
		}
	}
	
	public class GoogleSatelliteTileSource : TileSource {
		public GoogleSatelliteTileSource() {
			Name = "GoogleSatellite";
			FileExtension = ".png";
		}
		public override string GetTileUrl(TileName name) {
			return string.Format("http://khm1.google.com/kh/v=60&x={1}&s=&y={2}&z={0}&s=Gali",
			                     name.Zoom,
			                     name.X, name.Y);
		}
	}
	
	public class CycleMapTileSource : TileSource {
		public CycleMapTileSource() {
			Name = "CycleMap";
			FileExtension = ".png";
		}
		public override string GetTileUrl(TileName name) {
			return string.Format("http://c.andy.sandbox.cloudmade.com/tiles/cycle/{0}/{1}/{2}.png",
			                     name.Zoom,
			                     name.X, name.Y);
		}
	}
	
	public class BingTileSource : TileSource {
		public BingTileSource() {
			Name = "Bing";
			FileExtension = ".png";
		}
		protected string CalculateQuadKey(TileName name) {
			var quadKey = new System.Text.StringBuilder();
			var tileX = name.X;
			var tileY = name.Y;
            for (int i = name.Zoom; i > 0; i--)
            {
                char digit = '0';
                int mask = 1 << (i - 1);
                if ((tileX & mask) != 0)
                {
                    digit++;
                }
                if ((tileY & mask) != 0)
                {
                    digit++;
                    digit++;
                }
                quadKey.Append(digit);
            }
            return quadKey.ToString();
		}
		
		public override string GetTileUrl(TileName name) {
			return string.Format("http://ecn.t6.tiles.virtualearth.net/tiles/r{0}.png?g={1}&mkt=en-us&shading=hill&n=z",
			                     CalculateQuadKey(name),
			                     "452");
		}
	}
	
	public class BingSatelliteTileSource : BingTileSource {
		public BingSatelliteTileSource() {
			Name = "BingSatellite";
			FileExtension = ".png";
		}
		public override string GetTileUrl(TileName name) {
			return string.Format("http://ecn.t6.tiles.virtualearth.net/tiles/h{0}.png?g={1}&mkt=en-us&shading=hill&n=z",
			                     CalculateQuadKey(name),
			                     "452");
		}
	}
}
