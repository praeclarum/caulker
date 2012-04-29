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
using Caulker;
using OpenTK.Graphics.ES11;
using OpenTK;
using System.Drawing;
#if MONOTOUCH
using MonoTouch.UIKit;
#endif
#if MONODROID
using Android.Graphics;
#endif

namespace Caulker
{
	public class TileRenderer : IDrawable
	{
		TileTextureRepo _textures;
		
		TileCollection<Geometry> _geometries = new TileCollection<Geometry>();
		
		int MissingTileTexture = 0;
		
		TileSource _source;
		public TileSource Source { 
			get {
				return _source;
			}
			set {
				if (_source != null && _source.Name == value.Name) return;
				
				if (_textures != null) {
					_textures.Close();
				}
				_source = value;
				_textures = new TileTextureRepo(_source);
				_geometries.Clear();
			}
		}
		
		public TileRenderer ()
		{
			Source = new OpenStreetMapTileSource();
		}
		
		public void FreeMemory ()
		{
			Console.WriteLine ("OMG OMG OMG MEMORY");
			_textures.FreeMemory ();
		}
		
		int _lastZoom = -1;
		int _prevZoom = -1;
		DateTime _lastZoomFadeTime;
		const double FadeSecs = 1.0;
		
		public void StopDrawing ()
		{			
			_textures.Close();
		}
		
		public void LoadContent ()
		{
#if MONOTOUCH
			using (var img = UIImage.FromFile("MissingTile.png")) {
#endif
#if MONODROID
            using (var img = CaulkerUtils.MissingTile) {
#endif
				MissingTileTexture = img.ToGLTexture();
			}
		}
		
		public void Update (SimTime t)
		{
		}
		
		public void Draw (Camera cam, SimTime t)
		{			
			GL.Disable(All.DepthTest);
			GL.Enable(All.Texture2D);
			
			_textures.BeginFrame();
			_geometries.BeginFrame();
			
			GL.EnableClientState(All.TextureCoordArray);
			GL.EnableClientState(All.NormalArray);
			
			var zoom = cam.Zoom;
			
			if (_lastZoom < 0) _lastZoom = zoom;
			if (zoom != _lastZoom) {
				_prevZoom = _lastZoom;
				_lastZoomFadeTime = t.WallTime;
			}
			var fadeTime = t.WallTime - _lastZoomFadeTime;
			var shouldFade = fadeTime.TotalSeconds < FadeSecs;
			var fade = shouldFade ? (float)(fadeTime.TotalSeconds/FadeSecs) : 1.0f;
			_lastZoom = zoom;
			
			var centerTile1 = TileName.FromLocation(cam.LookAt, _prevZoom);
			var centerTile = TileName.FromLocation(cam.LookAt, zoom);
			
			//
			// Draw the tiles
			//
			if (shouldFade) {				
				DrawTiles(cam, centerTile1, 1.0f);
			}			
			DrawTiles(cam, centerTile, fade);			
			
			GL.DisableClientState(All.TextureCoordArray);
			GL.DisableClientState(All.NormalArray);
			GL.Disable(All.Texture2D);
			GL.Enable(All.DepthTest);
		}
		
		void DrawTiles(Camera cam, TileName centerTile, float alpha) {
			//
			// Determine the number of tiles to show
			//
			int minX=99999999, minY=99999999, maxX=0, maxY=0;
			for (var i = 0; i < cam.ScreenLocations.Length; i++) {
				var loc = cam.ScreenLocations[i];
				if (loc != null) {
					var c = TileName.FromLocation(loc, centerTile.Zoom);
					minX = Math.Min(c.X, minX);
					minY = Math.Min(c.Y, minY);
					maxX = Math.Max(c.X, maxX);
					maxY = Math.Max(c.Y, maxY);
				}
			}
			minX = Math.Min(centerTile.X, minX);
			minY = Math.Min(centerTile.Y, minY);
					maxX = Math.Max(centerTile.X, maxX);
					maxY = Math.Max(centerTile.Y, maxY);
			minX-=1;
			minY-=1;
			maxX+=1;
			maxY+=1;
			
			//
			// Draw them
			//
			var nd = 0;
			var tile = new TileName() { Zoom = centerTile.Zoom };
			
			var n = (int)Math.Pow(2, tile.Zoom);
			
			foreach (var p in RenderOrder) {
				tile.X = centerTile.X + p.X;
				tile.Y = centerTile.Y + p.Y;
				
				while (tile.X < 0) tile.X += n;
				while (tile.Y < 0) tile.Y += n;
				while (tile.X >= n) tile.X -= n;
				while (tile.Y >= n) tile.Y -= n;

				if (tile.X < minX || tile.Y < minY) continue;
				if (tile.X > maxX || tile.Y > maxY) continue;
				
				DrawTile(tile, alpha);
				nd ++;
			}
		}
		
		void DrawTile(TileName tile, float alpha) {
			var texture = _textures.GetTile(tile);
			
			var geo = GetGeometry(tile);
			
			if (texture == 0) {
				texture = MissingTileTexture;
			}
			GL.Color4(1.0f, 1.0f, 1.0f, alpha);
			GL.BindTexture(All.Texture2D, texture);
			GL.VertexPointer(3, All.Float, 0, geo.Verts);
			GL.TexCoordPointer(2, All.Float, 0, geo.TexVerts);
			GL.NormalPointer(All.Float, 0, geo.Norms);
			GL.DrawArrays(All.TriangleStrip, 0, geo.Verts.Length);
		}
		
		Geometry GetGeometry(TileName tile) {
			
			var g = _geometries[tile];
			
			if (g == null) {
				g = new Geometry();
				
				var locs = tile.GetCornerLocations();
				
				g.Verts = new OpenTK.Vector3[locs.Length];
				g.Norms = new OpenTK.Vector3[locs.Length];
				for (var i = 0; i < locs.Length; i++) {
					var p = locs[i].ToPositionAboveSeaLevel(0);
					g.Verts[i] = p;
					p.Normalize();
					g.Norms[i] = p;
					//Console.WriteLine (g.Verts[i]);
				}
				
				g.TexVerts = new Vector2[locs.Length];
				var up = Source.FlipVertical ? 0.0f : 1.0f;
				var down = 1.0f - up;
				g.TexVerts[0] = new Vector2(1.0f, up);
				g.TexVerts[1] = new Vector2(0, up);
				g.TexVerts[2] = new Vector2(1.0f, down);
				g.TexVerts[3] = new Vector2(0, down);
				
				_geometries[tile] = g;
			}
			
			return g;
		}
		
		
//		from random import shuffle
//s = 16
//pts = [(x,y) for x in range(-s,s) for y in range(-s,s)]
//shuffle(pts)
//def d(p):
//  return p[0]*p[0] + p[1]*p[1]
//pts = sorted(pts, key=d)
//for p in pts:
//  print "\t\t\tnew Point(%d,%d),"%p

		static System.Drawing.Point[] RenderOrder = new System.Drawing.Point[] {
			new System.Drawing.Point(0,0),
			new System.Drawing.Point(-1,0),
			new System.Drawing.Point(0,-1),
			new System.Drawing.Point(1,0),
			new System.Drawing.Point(0,1),
			new System.Drawing.Point(1,1),
			new System.Drawing.Point(1,-1),
			new System.Drawing.Point(-1,1),
			new System.Drawing.Point(-1,-1),
			new System.Drawing.Point(0,-2),
			new System.Drawing.Point(-2,0),
			new System.Drawing.Point(0,2),
			new System.Drawing.Point(2,0),
			new System.Drawing.Point(-2,1),
			new System.Drawing.Point(-1,-2),
			new System.Drawing.Point(2,-1),
			new System.Drawing.Point(1,-2),
			new System.Drawing.Point(-1,2),
			new System.Drawing.Point(2,1),
			new System.Drawing.Point(1,2),
			new System.Drawing.Point(-2,-1),
			new System.Drawing.Point(-2,2),
			new System.Drawing.Point(2,2),
			new System.Drawing.Point(2,-2),
			new System.Drawing.Point(-2,-2),
			new System.Drawing.Point(0,-3),
			new System.Drawing.Point(0,3),
			new System.Drawing.Point(3,0),
			new System.Drawing.Point(-3,0),
			new System.Drawing.Point(1,-3),
			new System.Drawing.Point(-3,1),
			new System.Drawing.Point(-1,-3),
			new System.Drawing.Point(1,3),
			new System.Drawing.Point(3,-1),
			new System.Drawing.Point(3,1),
			new System.Drawing.Point(-3,-1),
			new System.Drawing.Point(-1,3),
			new System.Drawing.Point(3,-2),
			new System.Drawing.Point(2,-3),
			new System.Drawing.Point(-2,3),
			new System.Drawing.Point(2,3),
			new System.Drawing.Point(-3,-2),
			new System.Drawing.Point(-2,-3),
			new System.Drawing.Point(-3,2),
			new System.Drawing.Point(3,2),
			new System.Drawing.Point(-4,0),
			new System.Drawing.Point(0,4),
			new System.Drawing.Point(4,0),
			new System.Drawing.Point(0,-4),
			new System.Drawing.Point(-1,4),
			new System.Drawing.Point(-4,1),
			new System.Drawing.Point(1,4),
			new System.Drawing.Point(-4,-1),
			new System.Drawing.Point(-1,-4),
			new System.Drawing.Point(1,-4),
			new System.Drawing.Point(4,-1),
			new System.Drawing.Point(4,1),
			new System.Drawing.Point(3,3),
			new System.Drawing.Point(3,-3),
			new System.Drawing.Point(-3,-3),
			new System.Drawing.Point(-3,3),
			new System.Drawing.Point(2,-4),
			new System.Drawing.Point(-2,4),
			new System.Drawing.Point(-4,2),
			new System.Drawing.Point(2,4),
			new System.Drawing.Point(-2,-4),
			new System.Drawing.Point(-4,-2),
			new System.Drawing.Point(4,2),
			new System.Drawing.Point(4,-2),
			new System.Drawing.Point(4,3),
			new System.Drawing.Point(-3,4),
			new System.Drawing.Point(-5,0),
			new System.Drawing.Point(4,-3),
			new System.Drawing.Point(-4,3),
			new System.Drawing.Point(-3,-4),
			new System.Drawing.Point(0,-5),
			new System.Drawing.Point(3,-4),
			new System.Drawing.Point(-4,-3),
			new System.Drawing.Point(3,4),
			new System.Drawing.Point(0,5),
			new System.Drawing.Point(5,0),
			new System.Drawing.Point(-5,-1),
			new System.Drawing.Point(-1,-5),
			new System.Drawing.Point(1,5),
			new System.Drawing.Point(1,-5),
			new System.Drawing.Point(5,-1),
			new System.Drawing.Point(-5,1),
			new System.Drawing.Point(-1,5),
			new System.Drawing.Point(5,1),
			new System.Drawing.Point(5,-2),
			new System.Drawing.Point(5,2),
			new System.Drawing.Point(2,-5),
			new System.Drawing.Point(2,5),
			new System.Drawing.Point(-2,-5),
			new System.Drawing.Point(-5,-2),
			new System.Drawing.Point(-5,2),
			new System.Drawing.Point(-2,5),
			new System.Drawing.Point(4,4),
			new System.Drawing.Point(-4,-4),
			new System.Drawing.Point(4,-4),
			new System.Drawing.Point(-4,4),
			new System.Drawing.Point(5,3),
			new System.Drawing.Point(3,5),
			new System.Drawing.Point(3,-5),
			new System.Drawing.Point(-5,3),
			new System.Drawing.Point(-3,-5),
			new System.Drawing.Point(5,-3),
			new System.Drawing.Point(-3,5),
			new System.Drawing.Point(-5,-3),
			new System.Drawing.Point(0,6),
			new System.Drawing.Point(6,0),
			new System.Drawing.Point(-6,0),
			new System.Drawing.Point(0,-6),
			new System.Drawing.Point(1,-6),
			new System.Drawing.Point(-1,-6),
			new System.Drawing.Point(-6,1),
			new System.Drawing.Point(6,-1),
			new System.Drawing.Point(6,1),
			new System.Drawing.Point(-1,6),
			new System.Drawing.Point(-6,-1),
			new System.Drawing.Point(1,6),
			new System.Drawing.Point(6,2),
			new System.Drawing.Point(2,6),
			new System.Drawing.Point(2,-6),
			new System.Drawing.Point(-2,6),
			new System.Drawing.Point(6,-2),
			new System.Drawing.Point(-2,-6),
			new System.Drawing.Point(-6,2),
			new System.Drawing.Point(-6,-2),
			new System.Drawing.Point(-5,-4),
			new System.Drawing.Point(-4,-5),
			new System.Drawing.Point(4,-5),
			new System.Drawing.Point(-5,4),
			new System.Drawing.Point(5,-4),
			new System.Drawing.Point(4,5),
			new System.Drawing.Point(5,4),
			new System.Drawing.Point(-4,5),
			new System.Drawing.Point(6,3),
			new System.Drawing.Point(-6,3),
			new System.Drawing.Point(3,-6),
			new System.Drawing.Point(6,-3),
			new System.Drawing.Point(-3,-6),
			new System.Drawing.Point(-6,-3),
			new System.Drawing.Point(3,6),
			new System.Drawing.Point(-3,6),
			new System.Drawing.Point(-7,0),
			new System.Drawing.Point(0,-7),
			new System.Drawing.Point(0,7),
			new System.Drawing.Point(7,0),
			new System.Drawing.Point(1,-7),
			new System.Drawing.Point(-1,-7),
			new System.Drawing.Point(-7,1),
			new System.Drawing.Point(1,7),
			new System.Drawing.Point(-5,-5),
			new System.Drawing.Point(-5,5),
			new System.Drawing.Point(5,-5),
			new System.Drawing.Point(-7,-1),
			new System.Drawing.Point(5,5),
			new System.Drawing.Point(7,1),
			new System.Drawing.Point(-1,7),
			new System.Drawing.Point(7,-1),
			new System.Drawing.Point(-4,-6),
			new System.Drawing.Point(4,6),
			new System.Drawing.Point(6,-4),
			new System.Drawing.Point(-6,4),
			new System.Drawing.Point(-4,6),
			new System.Drawing.Point(4,-6),
			new System.Drawing.Point(-6,-4),
			new System.Drawing.Point(6,4),
			new System.Drawing.Point(-2,-7),
			new System.Drawing.Point(-7,-2),
			new System.Drawing.Point(2,7),
			new System.Drawing.Point(7,2),
			new System.Drawing.Point(-2,7),
			new System.Drawing.Point(7,-2),
			new System.Drawing.Point(2,-7),
			new System.Drawing.Point(-7,2),
			new System.Drawing.Point(-7,-3),
			new System.Drawing.Point(7,-3),
			new System.Drawing.Point(3,-7),
			new System.Drawing.Point(3,7),
			new System.Drawing.Point(-3,7),
			new System.Drawing.Point(-3,-7),
			new System.Drawing.Point(7,3),
			new System.Drawing.Point(-7,3),
			new System.Drawing.Point(6,5),
			new System.Drawing.Point(-5,-6),
			new System.Drawing.Point(-5,6),
			new System.Drawing.Point(6,-5),
			new System.Drawing.Point(5,6),
			new System.Drawing.Point(-6,5),
			new System.Drawing.Point(5,-6),
			new System.Drawing.Point(-6,-5),
			new System.Drawing.Point(0,-8),
			new System.Drawing.Point(-8,0),
			new System.Drawing.Point(-4,7),
			new System.Drawing.Point(-7,4),
			new System.Drawing.Point(-1,-8),
			new System.Drawing.Point(4,7),
			new System.Drawing.Point(-8,-1),
			new System.Drawing.Point(-8,1),
			new System.Drawing.Point(4,-7),
			new System.Drawing.Point(7,4),
			new System.Drawing.Point(-4,-7),
			new System.Drawing.Point(7,-4),
			new System.Drawing.Point(1,-8),
			new System.Drawing.Point(-7,-4),
			new System.Drawing.Point(-8,-2),
			new System.Drawing.Point(-2,-8),
			new System.Drawing.Point(2,-8),
			new System.Drawing.Point(-8,2),
			new System.Drawing.Point(-6,-6),
			new System.Drawing.Point(6,6),
			new System.Drawing.Point(-6,6),
			new System.Drawing.Point(6,-6),
			new System.Drawing.Point(3,-8),
			new System.Drawing.Point(-8,3),
			new System.Drawing.Point(-3,-8),
			new System.Drawing.Point(-8,-3),
			new System.Drawing.Point(-7,5),
			new System.Drawing.Point(7,5),
			new System.Drawing.Point(-7,-5),
			new System.Drawing.Point(-5,7),
			new System.Drawing.Point(7,-5),
			new System.Drawing.Point(5,7),
			new System.Drawing.Point(5,-7),
			new System.Drawing.Point(-5,-7),
			new System.Drawing.Point(4,-8),
			new System.Drawing.Point(-8,4),
			new System.Drawing.Point(-4,-8),
			new System.Drawing.Point(-8,-4),
			new System.Drawing.Point(-7,-6),
			new System.Drawing.Point(-6,-7),
			new System.Drawing.Point(6,-7),
			new System.Drawing.Point(-6,7),
			new System.Drawing.Point(7,6),
			new System.Drawing.Point(6,7),
			new System.Drawing.Point(7,-6),
			new System.Drawing.Point(-7,6),
			new System.Drawing.Point(-8,-5),
			new System.Drawing.Point(-5,-8),
			new System.Drawing.Point(-8,5),
			new System.Drawing.Point(5,-8),
			new System.Drawing.Point(-7,7),
			new System.Drawing.Point(7,-7),
			new System.Drawing.Point(-7,-7),
			new System.Drawing.Point(7,7),
			new System.Drawing.Point(-8,6),
			new System.Drawing.Point(-8,-6),
			new System.Drawing.Point(-6,-8),
			new System.Drawing.Point(6,-8),
			new System.Drawing.Point(-7,-8),
			new System.Drawing.Point(-8,7),
			new System.Drawing.Point(-8,-7),
			new System.Drawing.Point(7,-8),
			new System.Drawing.Point(-8,-8),
		};
		
	}
}
