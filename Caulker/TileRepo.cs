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

using System;
using OpenTK.Graphics.ES11;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using MonoTouch.UIKit;
using MonoTouch.CoreGraphics;
using System.Drawing;
using System.Runtime.InteropServices;
using Caulker;

namespace Caulker
{
	public class TileName {
		public int Zoom, X, Y;
		
		public static TileName FromLocation(ILocatable loc, int zoom) {
			var n = Math.Pow(2, zoom);
			var latRad = loc.Latitude * Math.PI / 180;			
			var xtile = ((loc.Longitude + 180) / 360) * n;			
			var ytile = ((1 - (Math.Log(Math.Tan(latRad) + 1/Math.Cos(latRad)) / Math.PI)) / 2) * n;
			return new TileName() {
				Zoom = zoom,
				X = (int)xtile,
				Y = (int)ytile
			};
		}
		
		public TileName Clone() {
			return new TileName() {
				Zoom = Zoom,
				X = X,
				Y = Y
			};
		}
		
		public static Location GetLocation(int zoom, int x, int y) {
			var n = Math.Pow(2, zoom);
			var lon = (x / n) * 360 - 180;			
			var lat = Math.Atan(Math.Sinh(Math.PI*(1 - 2*y/n))) * 180 / Math.PI;			
			var loc = new Location(lon, lat);
			//Console.WriteLine ("{0},{1} -> {2}", x,y,loc);
			return loc;
		}

		public Location[] GetCornerLocations() {
			var locs = new Location[4];
			locs[0] = GetLocation(Zoom, X+1, Y+1);
			locs[1] = GetLocation(Zoom, X, Y+1);
			locs[2] = GetLocation(Zoom, X+1, Y);
			locs[3] = GetLocation(Zoom, X, Y);
			return locs;
		}
		
		public override bool Equals (object obj)
		{
			var o = obj as TileName;
			return (o != null) && (o.Zoom == Zoom) && (o.X == X) && (o.Y == Y);
		}
		
		public override int GetHashCode ()
		{
			return (Zoom + X + Y).GetHashCode();
		}

		public override string ToString ()
		{
			return string.Format("[Tile Zoom={0} ({1}, {2})]", Zoom, X, Y);
		}
	}
	
	public class TileCollectionItem {
		/// <summary>
		/// Priority. The lower this number, the higher the priority. I know. Sorry.
		/// </summary>
		public ulong Priority;
		public TileName Name;
		public object Value;
	}
		
	public class TileCollection<T> {
		Dictionary<ulong, TileCollectionItem> _tiles;		
		
		uint _curFrame;
		ulong _nextPriority;
		
		const uint NumPrioritiesPerFrame = 10000;
		
		public TileCollection() {
			_tiles = new Dictionary<ulong, TileCollectionItem>();
			_curFrame = 0;
			_nextPriority = 0;
			BeginFrame();
		}

		public int Count { get { return _tiles.Count; } }
		
		public void BeginFrame() {
			_curFrame++;		
			// 1,000,000,000 = Total Possible Frames
			// At 60 FPS, = 192 days
			_nextPriority = (1000000000UL - _curFrame) * NumPrioritiesPerFrame;
		}
		
		public T this[TileName name] {
			get {
				TileCollectionItem t = null;
				if (_tiles.TryGetValue(GetId(name), out t)) {
					t.Priority = _nextPriority++;
					return (T)t.Value;
				}
				else {
					return default(T);
				}
			}
			set {
				var t = new TileCollectionItem() {
					Priority = _nextPriority++,
					Name = name.Clone(),
					Value = value
				};
				_tiles[GetId(name)] = t;
			}
		}
		
		public TileCollectionItem GetLowestPriority() {
			TileCollectionItem tile = null;
			foreach (var t in _tiles.Values) {
				if (tile == null || t.Priority > tile.Priority) {
					tile = t;
				}
			}
			return tile;
		}
		
		public TileCollectionItem GetHighestPriority() {
			TileCollectionItem tile = null;
			foreach (var t in _tiles.Values) {
				if (tile == null || t.Priority < tile.Priority) {
					tile = t;
				}
			}
			return tile;
		}
		
		public void Remove(TileName tile) {
			var key = GetId(tile);
			_tiles.Remove(key);
		}
		
		public void Clear() {
			_tiles.Clear();
		}
		
		public void ForEach(Action<T> action) {
			foreach (var v in _tiles.Values) {
				action((T)v.Value);
			}
		}
		
		static ulong GetId(TileName name) {
			// 24 bits to x, y (48, only need 36 total)
			// 16 bits to zoom (16, only need 5)
			var ux = (ulong)name.X;
			var uy = (ulong)name.Y;
			var uz = (ulong)name.Zoom;
			return (uz << 48) | (ux << 24) | uy;
		}
	}

	public class TileTextureRepo
	{
		const int MaxInMemoryTextures = 150;
		
		string DataDir;
		
		TileSource _source;
		
		readonly TileCollection<int> _glTiles = new TileCollection<int>();
		readonly TileCollection<TextureData> _loadedTiles = new TileCollection<TextureData>();
		readonly TileCollection<bool> _onDiskTiles = new TileCollection<bool>();		
		readonly TileCollection<bool> _downloadingTiles = new TileCollection<bool>();
		readonly TileCollection<bool> _loadingTiles = new TileCollection<bool>();
		
		bool _continueWorkingInBackground = false;
		AutoResetEvent _wakeupDownloader = new AutoResetEvent(false);
		AutoResetEvent _wakeupLoader = new AutoResetEvent(false);
		
		public TileTextureRepo (TileSource source)
		{
			DataDir = Path.GetFullPath("../Library/Cache/TileData");
			if (!Directory.Exists(DataDir)) {
				Directory.CreateDirectory(DataDir);
			}
			_source = source;
			FindDiskTiles();
			_continueWorkingInBackground = true;
			BeginFrame();

			var th = new Thread((ThreadStart)DownloadThread);
			th.Start();
			
			var lth = new Thread((ThreadStart)LoadThread);
			lth.Start();
		}
		
		public void Close() {
			_continueWorkingInBackground = false;
			RemoveTilesFromMemory();			
		}
		
		public void FreeMemory() {
			RemoveTilesFromMemory();
		}
		
		public void BeginFrame() {			
			_glTiles.BeginFrame();
			_downloadingTiles.BeginFrame();
			_loadingTiles.BeginFrame();
			_loadedTiles.BeginFrame();
			//Console.WriteLine ("--");
		}
		
		public int GetTile(TileName name) {
			
			//
			// Try to get it from the in-memory store
			//
			var r = _glTiles[name];
			if (r != 0) {
				return r;
			}
			
			//
			// If it's not in memory, let's see if its data has been loaded
			//
			TextureData data = null;
			lock (_loadedTiles) {
				data = _loadedTiles[name];
			}
			if (data != null) {
				r = data.CreateGLTexture();
				
				lock (_loadedTiles) {
					_loadedTiles.Remove(name);				
				}
				data.Dispose();
				
				//
				// Clear memory if needed
				//
				if (_glTiles.Count >= MaxInMemoryTextures) {
					var oldest = _glTiles.GetLowestPriority();
					_glTiles.Remove(oldest.Name);
					//Console.WriteLine ("Free " + _glTiles.Count);
					DeleteTextureMemory((int)oldest.Value);
				}
				_glTiles[name] = r;
				return r;
			}
			
			//
			// Have we already requested that this tile be loaded from disk?
			//
			lock (_loadingTiles) {
				var loading = _loadingTiles[name];
				
				if (loading) {
					// Already queued, just need to be patient
					return 0;
				}
			}

			//
			// OK, we've never even tried to load it before, try to get it from disk
			//
			var onDisk = false;
			lock (_onDiskTiles) {
				onDisk = _onDiskTiles[name];
			}
			if (onDisk) {
				lock (_loadingTiles) {
					_loadingTiles[name] = true;					
				}
				_wakeupLoader.Set();
				
				return 0;
			}
			
			//
			// It's not on disk either! Better ask for it to be downloaded
			//
			lock (_downloadingTiles) {
				var d = _downloadingTiles[name];
				if (!d) {
					_downloadingTiles[name] = true;
					_wakeupDownloader.Set();
				}
			}
			
			return 0; // We've got nothing, for now
		}
		
		string GetTilePath(TileName name) {
			var filename = _source.Name + "-" + name.Zoom + "-" + name.X + "-" + name.Y + _source.FileExtension;
			return Path.Combine(DataDir, filename);
		}
		
		void FindDiskTiles() {
			var files = Directory.GetFiles(DataDir);
			var fileNameRe = new Regex(_source.Name + @"-(\d+)-(\d+)-(\d+)\.");
			
			lock (_onDiskTiles) {
				_onDiskTiles.Clear();
				
				foreach (var f in files) {
					var m = fileNameRe.Match(f);
					if (m.Success) {
						int zoom=0, x = 0, y = 0;
						if (int.TryParse(m.Groups[1].Value, out zoom) &&
						    int.TryParse(m.Groups[2].Value, out x) &&
						    int.TryParse(m.Groups[3].Value, out y)) {
							var name = new TileName {
								X = x,
								Y = y,
								Zoom = zoom
							};
							_onDiskTiles[name] = true;
						}
					}
				}
				Console.WriteLine ("Found {0} on disk tiles for {1}", _onDiskTiles.Count, _source.Name);
			}
		}
		
		void LoadThread() {
			
			using (var pool = new MonoTouch.Foundation.NSAutoreleasePool()) {
			
				while (_continueWorkingInBackground) {
					TileCollectionItem q = null;
					lock (_loadingTiles) {
						q = _loadingTiles.GetHighestPriority();
					}
					
					if (q == null) {
						_wakeupLoader.WaitOne(TimeSpan.FromSeconds(5));
					}
					else {
						
						var req = q.Name;
						
						try {
							var filename = GetTilePath(req);
							using (var image = UIImage.FromFileUncached(filename)) {
								
								if (image != null) {
									var data = TextureData.FromUIImage(image);
									lock (_loadedTiles) {
										_loadedTiles[req] = data;
									}
								}
								else {
									Console.WriteLine ("! Failed to load " + req);
									throw new Exception();
								}
							}
						}
						catch (Exception) {
							//
							// If there was an error loading the tile, then it
							// must be bad. Redownload it.
							//
							lock (_onDiskTiles) {
								_onDiskTiles.Remove(req);
							}
						}
						finally {
							lock (_loadingTiles) {
								_loadingTiles.Remove(req);
							}
						}
					}
				}
			}
		}
		
		void DownloadThread() {
			
			using (var pool = new MonoTouch.Foundation.NSAutoreleasePool()) {
				
				while (_continueWorkingInBackground) {
					TileCollectionItem q;
					lock (_downloadingTiles) {
						q = _downloadingTiles.GetHighestPriority();
					}
					
					if (q == null) {
						_wakeupDownloader.WaitOne(TimeSpan.FromSeconds(5));
					}
					else {
						var req = q.Name;
						var url = _source.GetTileUrl(req);
						var filename = GetTilePath(req);
						var tempFile = Path.GetTempFileName();
						
						var downloadedSuccessfully = Http.Download(url, tempFile);
						
						if (downloadedSuccessfully) {
							try {
								
								File.Move(tempFile, filename);
								
								lock (_onDiskTiles) {
									_onDiskTiles[req] = true;
								}
								lock (_downloadingTiles) {
									_downloadingTiles.Remove(req);
								}
	
							}
							catch (Exception) {							
							}
						}
						else {
							// Download failed, let's chill out for a bit
							_wakeupDownloader.WaitOne(TimeSpan.FromSeconds(5));
						}
					}
				}
			}
		}
		
		void RemoveTilesFromMemory() {
			_glTiles.ForEach(DeleteTextureMemory);
			_glTiles.Clear();
			lock (_loadedTiles) {
				_loadedTiles.ForEach(data => {
					data.Dispose();
				});
				_loadedTiles.Clear();
			}
		}
		
		void DeleteTextureMemory(int texture) {
			GL.DeleteTextures(1, new int[1] { texture });
		}				
	}
	
	public enum TexturePixelFormat {
		RGBA8888,
		RGB565,
		A8
	}

	public class TextureData : IDisposable {
		public IntPtr Data { get; private set; }
		public TexturePixelFormat PixelFormat { get; private set; }
		public int Width { get; private set; }
		public int Height { get; private set; }
		
		public TextureData(IntPtr data, TexturePixelFormat pixelFormat, int width, int height) {
			Data = data;
			PixelFormat = pixelFormat;
			Width = width;
			Height = height;
		}
		
		public void Dispose() {
			if (Data != IntPtr.Zero) {
				Marshal.FreeHGlobal(Data);
				Data = IntPtr.Zero;
			}
		}
		
		public int CreateGLTexture() {
			var textures = new int[1];
			GL.GenTextures(1, textures);
			var texture = textures[0];
			if (texture == 0) return 0;			
			
			GL.BindTexture(All.Texture2D, texture);
			
			GL.TexParameter(All.Texture2D, All.TextureMinFilter, (int)All.Linear);
			GL.TexParameter(All.Texture2D, All.TextureMagFilter, (int)All.Linear);
			GL.TexParameter(All.Texture2D, All.TextureWrapS, (int)All.ClampToEdge);
			GL.TexParameter(All.Texture2D, All.TextureWrapT, (int)All.ClampToEdge);
			
			//GL.TexParameter(All.Texture2D, All.TextureMinFilter, (int)All.LinearMipmapNearest);
			//GL.TexParameter(All.Texture2D, All.GenerateMipmap, 1);
			
			switch (PixelFormat) {		
			case TexturePixelFormat.RGBA8888:
				GL.TexImage2D(All.Texture2D, 0, (int)All.Rgba, Width, Height, 0, All.Rgba, All.UnsignedByte, Data);
				break;
			case TexturePixelFormat.RGB565:
				GL.TexImage2D(All.Texture2D, 0, (int)All.Rgb, Width, Height, 0, All.Rgb, All.UnsignedShort565, Data);
				break;				
			case TexturePixelFormat.A8:
				GL.TexImage2D(All.Texture2D, 0, (int)All.Alpha, Width, Height, 0, All.Alpha, All.UnsignedByte, Data);
				break;				
			default:
				return 0;
			}
			
			return texture;
		}
		
		public static TextureData FromUIImage(UIImage uiImage) {
			if (uiImage == null) throw new ArgumentNullException("uiImage");
			var image = uiImage.CGImage;
			if (image == null) throw new ArgumentNullException("uiImage.CGImage");
			
			var info = image.AlphaInfo;
			
			var hasAlpha = ((info == CGImageAlphaInfo.PremultipliedLast) || (info == CGImageAlphaInfo.PremultipliedFirst) || (info == CGImageAlphaInfo.Last) || (info == CGImageAlphaInfo.First));
			
			var pixelFormat = TexturePixelFormat.RGBA8888;
			if (image.ColorSpace != null) {
				if (hasAlpha)
					pixelFormat = TexturePixelFormat.RGBA8888;
				else
					pixelFormat = TexturePixelFormat.RGB565;
			} else { //NOTE: No colorspace means a mask image
				pixelFormat = TexturePixelFormat.A8;
			}	
	
			var width = image.Width;
			var height = image.Height;
			
			var data = IntPtr.Zero;
			CGBitmapContext context = null;
			
			switch (pixelFormat) {		
			case TexturePixelFormat.RGBA8888:
				using (var colorSpace = CGColorSpace.CreateDeviceRGB()) {
					data = Marshal.AllocHGlobal(height * width * 4);
					context = new CGBitmapContext(data, width, height, 8, 4 * width, colorSpace, 
					                              (CGImageAlphaInfo)((uint)CGImageAlphaInfo.PremultipliedLast | (uint)CGBitmapFlags.ByteOrder32Big));
				}
				break;
			case TexturePixelFormat.RGB565:
				using (var colorSpace = CGColorSpace.CreateDeviceRGB()) {
					data = Marshal.AllocHGlobal(height * width * 4);
					context = new CGBitmapContext(data, width, height, 8, 4 * width, colorSpace, 
					                              (CGImageAlphaInfo)((uint)CGImageAlphaInfo.NoneSkipLast | (uint)CGBitmapFlags.ByteOrder32Big));
				}
				break;				
			case TexturePixelFormat.A8:
				data = Marshal.AllocHGlobal(height * width);
				context = new CGBitmapContext(data, width, height, 8, width, null, CGImageAlphaInfo.Only);
				break;				
			default:
				throw new NotSupportedException(pixelFormat + " is not supported");
			}
			
			context.ClearRect(new RectangleF(0, 0, width, height));	
			context.DrawImage(new RectangleF(0, 0, width, height), image);
			context.Dispose();
			
			if (pixelFormat == TexturePixelFormat.RGB565) {
				var tempData = Marshal.AllocHGlobal(height * width * 2);
				unsafe {
					var inPixel32 = (uint*)data;
					var outPixel16 = (ushort*)tempData;
					for (var i = 0; i < width * height; ++i, ++inPixel32) {
						*outPixel16++ = (ushort)(((((*inPixel32 >> 0) & 0xFF) >> 3) << 11) | 
							            ((((*inPixel32 >> 8) & 0xFF) >> 2) << 5) | 
								        ((((*inPixel32 >> 16) & 0xFF) >> 3) << 0));
					}
				}
				Marshal.FreeHGlobal(data);
				data = tempData;			
			}
			
			return new TextureData(data, pixelFormat, width, height);
		}		

	}

	public static class GLEx {
		
		public static int ToGLTexture(this UIImage uiImage) {
			using (var d = TextureData.FromUIImage(uiImage)) {
				return d.CreateGLTexture();
			}
		}
		
	}
}
