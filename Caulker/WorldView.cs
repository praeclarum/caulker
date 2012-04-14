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
using System.Drawing;
using System.Linq;
using MonoTouch.CoreAnimation;
using MonoTouch.Foundation;
using MonoTouch.OpenGLES;
using MonoTouch.ObjCRuntime;
using OpenTK;
using OpenTK.Graphics.ES11;
using OpenTK.Platform.iPhoneOS;

using MonoTouch.UIKit;
using System.Collections.Generic;
using Caulker;

namespace Caulker {
	
	public class SimTime
	{
		public DateTime Time { get; set; }
		public DateTime WallTime { get; set; }
		public double TimeElapsed { get; set; }
		public double WallTimeElapsed { get; set; }
	}

	public interface IDrawable
	{
		void Draw (Camera cam, SimTime t);
		void OnStopDrawing ();
	}
		
	[MonoTouch.Foundation.Register("WorldView")]
	public class WorldView : iPhoneOSGameView
	{
		uint _depthRenderbuffer;

		double _lastT = new NSDate().SecondsSinceReferenceDate;		

		Background _background = new Background();
	
		List<IDrawable> _draws = new List<IDrawable>();
		
		public Camera Camera { get; private set; }
		public CameraMan CameraMan { get; set; }
		
		Location _sunLoc;
		public bool ShowSun { get; set; }
		
		[Export ("layerClass")]
		public static Class LayerClass()
		{
			return iPhoneOSGameView.GetLayerClass ();
		}
	
		[Export ("initWithCoder:")]
		public WorldView (NSCoder coder) : base (coder)
		{
			Initialize();
		}
		
		public WorldView (RectangleF frame) : base (frame)
		{
			Initialize();
		}

		protected override void ConfigureLayer(CAEAGLLayer eaglLayer)
		{
			eaglLayer.Opaque = true;
		}
		
	    void Initialize ()
		{
			LayerRetainsBacking = true;
			LayerColorFormat    = EAGLColorFormat.RGBA8;
			ContextRenderingApi = EAGLRenderingAPI.OpenGLES1;
			
			MultipleTouchEnabled = true;
			
			var now = DateTime.UtcNow;
			_sunLoc = Location.SunLocation(now);
	        Camera = new Camera();
			CameraMan = new BlimpCameraMan(
	            new Location(-122.3352, 47.645),
	            new Location(-122.3352, 47.6453));
			Unload += HandleUnload;
	    }

        void HandleUnload (object sender, EventArgs e)
        {
			foreach (var d in _draws) {
				d.OnStopDrawing();
			}
        }
		
		protected override void CreateFrameBuffer ()
		{
			base.CreateFrameBuffer ();
			
			//
			// Enable the depth buffer
			//
			var sz = Size;
			GL.Oes.GenRenderbuffers(1, ref _depthRenderbuffer);
			GL.Oes.BindRenderbuffer(All.RenderbufferOes, _depthRenderbuffer);
			GL.Oes.RenderbufferStorage(All.RenderbufferOes, All.DepthComponent16Oes, sz.Width, sz.Height);
			GL.Oes.FramebufferRenderbuffer(All.FramebufferOes, All.DepthAttachmentOes, All.RenderbufferOes, _depthRenderbuffer);
		}
		
		#region DisplayLink support
		
		int _frameInterval = 0;
		CADisplayLink _displayLink;
		bool _isRendering;
				
		public void StartRendering ()
		{
			if (_isRendering)
				return;
			
			CreateFrameBuffer ();
			CADisplayLink displayLink = UIScreen.MainScreen.CreateDisplayLink (this, new Selector ("drawFrame"));
			displayLink.FrameInterval = _frameInterval;
			displayLink.AddToRunLoop (NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
			_displayLink = displayLink;
			
			_isRendering = true;
		}
		
		public void StopRendering ()
		{
			if (!_isRendering)
				return;
			_displayLink.Invalidate ();
			_displayLink = null;
			DestroyFrameBuffer ();
			_isRendering = false;
		}
		
		[Export ("drawFrame")]
		void DrawFrame ()
		{
			OnRenderFrame (new FrameEventArgs ());
		}
		
		#endregion
		
		#region Drawing

		protected override void OnRenderFrame (FrameEventArgs e)
		{
			base.OnRenderFrame (e);
			
			var sz = new Size((int)Frame.Width, (int)Frame.Height);
			if (sz.Width != Size.Width) {
				Size = sz;
			}
			
			MakeCurrent();
			GL.Viewport (0, 0, Size.Width, Size.Height);
			
			var t = new NSDate().SecondsSinceReferenceDate;
			var wallNow = DateTime.UtcNow;
			
			var time = new SimTime() {
				Time = wallNow,
				WallTime = wallNow,
				TimeElapsed = t - _lastT,
				WallTimeElapsed = t - _lastT
			};
			_lastT = t;
			
			//Console.WriteLine ("FPS {0:0}", 1.0 / time.WallTimeElapsed);
	
			GL.ClearColor (158/255.0f, 207/255.0f, 237/255.0f, 1.0f);
			GL.Clear((int)(All.DepthBufferBit | All.ColorBufferBit));
			
			//
			// Set the common OpenGL state
			//
	        GL.Enable(All.Blend);
	        GL.BlendFunc(All.SrcAlpha, All.OneMinusSrcAlpha);
			GL.Enable(All.DepthTest);
	        GL.EnableClientState(All.VertexArray);
			
			//
			// Render the background
			//
			_background.Render();
			
			//
			// Setup the 3D camera
			//
			Camera.SetViewport(Size.Width, Size.Height);
	        CameraMan.Update(time);
	        Camera.Execute(CameraMan);
			
			//
			// Enable the sun
			//
			if (ShowSun) {
				GL.Enable(All.Lighting);
				GL.Enable(All.ColorMaterial);
			
				GL.Enable(All.Light0);
				var sp = _sunLoc.ToPositionAboveSeaLevel(150000000);
				GL.Light(All.Light0, All.Position, new float[]{sp.X,sp.Y,sp.Z,1});
			}
			
			//
			// Draw all the layers
			//
			foreach (var d in _draws) {
				d.Draw(Camera, time);
			}

			if (ShowSun) {
				GL.Disable(All.Lighting);
				GL.Disable(All.ColorMaterial);
				
//				if (_gesture == WorldView3d.GestureType.Rotating) {
//					var verts = new Vector3[2];
//					var ppp = Location.SunLocation(DateTime.UtcNow.AddHours(-12));
//					verts[0] = ppp.ToPositionAboveSeaLevel(0);
//					verts[1] = ppp.ToPositionAboveSeaLevel(1000);
//					GL.Color4(0, 1.0f, 0, 1.0f);
//					GL.VertexPointer(3, All.Float, 0, verts);
//					GL.DrawArrays(All.Lines, 0, 2);			
//					Console.WriteLine (Camera.LookAt);
//					Console.WriteLine (ppp);
//				}

			}

			SwapBuffers();
		}
		
		public void AddDrawable (IDrawable drawable)
		{
			_draws.Add(drawable);
		}
		
		#endregion
	    		
		#region Gesture recognition
				
		enum GestureType {
			None,
			Panning,
			Pitching,
			RotatingScaling,
		}
		GestureType _gesture = GestureType.None;
		
		ManualCameraMan ForceManualCameraMan() {
			var man = CameraMan as ManualCameraMan;
			if (man == null) {
				man = new ManualCameraMan(CameraMan.Pos3d.ToLocation(), CameraMan.LookAt);
				CameraMan = man;
			}
			return man;
		}
		
		Dictionary<IntPtr, UITouch> _activeTouches = new Dictionary<IntPtr, UITouch>();
		
		public override void TouchesBegan (NSSet touches, UIEvent evt)
		{
			var ts = touches.ToArray<UITouch>();
			foreach (var t in ts) {
				_activeTouches[t.Handle] = t;
			}
		}

		public override void TouchesEnded (NSSet touches, UIEvent evt)
		{
			var ts = touches.ToArray<UITouch>();
			foreach (var t in ts) {
				_activeTouches.Remove(t.Handle);
			}
			if (_activeTouches.Count == 0) {
				_gesture = GestureType.None;
			}
		}

		public override void TouchesMoved (NSSet touches, UIEvent evt)
		{
			//Console.WriteLine (_gesture);
			//var ts = touches.ToArray<UITouch>();

			// Make sure we have a camera man that can take orders
			var man = ForceManualCameraMan();
			
			var ts = _activeTouches.Values.ToArray();
	
			if (_activeTouches.Count == 1) {
				var t = ts[0];
				var start = Camera.GetLocation(t.PreviousLocationInView(this));
				var end = Camera.GetLocation(t.LocationInView(this));
				if (start != null && end != null) {
					man.Drag(start, end);
				}
			}
			else if (_activeTouches.Count == 2) {
				var ppt0 = ts[0].PreviousLocationInView(this);
				var ppt1 = ts[1].PreviousLocationInView(this);
				
				var pt0 = ts[0].LocationInView(this);
				var pt1 = ts[1].LocationInView(this);

				var dpt0 = ppt0.VectorTo(pt0);
				var dpt1 = ppt1.VectorTo(pt1);

				var pd = ppt0.VectorTo(ppt1);
				var d = pt0.VectorTo(pt1);

				var cpt = new PointF(pt0.X + d.X/2, pt0.Y + d.Y/2);
				var cloc = Camera.GetLocation(cpt);

				var pr = pd.Length;
				var r = d.Length;
				
				var pa = Math.Atan2(pd.Y, pd.X) * 180 / Math.PI;
				var a = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
				
				// Transition from None
				if (_gesture == GestureType.None) {
					if ((dpt0.Y < 0 && dpt1.Y < 0) ||
					    (dpt0.Y > 0 && dpt1.Y > 0)) {
						
						_gesture = GestureType.Pitching;
					}
					else {
						_gesture = GestureType.RotatingScaling;
					}
				}

				// Respond to the gesture
				if (_gesture == GestureType.RotatingScaling) {
					man.Rotate(cloc, (float)(a - pa));
					if (r > 0 && pr > 0) {
						man.Scale(cloc, r / pr);
					}
				}
				else if (_gesture == GestureType.Pitching) {
					var pitch = Math.Abs(dpt0.Y/2 + dpt1.Y/2);
					man.Pitch(pitch / Size.Height);
				}
			}
		}
		
		#endregion
    }
	
    public class Geometry {
        public Vector3[] Verts;
		public Vector3[] Norms;
		public Vector2[] TexVerts;
    }

    public class Background
    {
        float[] BackgroundVerts = new float[] {
                1,-1, 
				-1,-1, 
				1,-0.5f, 
				-1,-0.5f, 
				1,0f, 
				-1,0, 
				1,1, 
				-1,1
            };
        byte[] BackgroundColors = new byte[] {
                158,207,237,255,
                158,207,237,255,
                38,77,144,255,
                38,77,144,255,
                17,37,78,255,
                17,37,78,255,
                3,14,31,255,
                3,14,31,255,
            };
		public Background() {
			for (var i = 1; i < BackgroundVerts.Length; i+=2) {
				BackgroundVerts[i] = BackgroundVerts[i]/2 + 0.5f;
			}
		}
        public void Render() {
			
			GL.Disable(All.DepthTest);
			
            GL.MatrixMode(All.Projection);
            GL.LoadIdentity();
            GL.MatrixMode(All.Modelview);
            GL.LoadIdentity();

            GL.VertexPointer(2, All.Float, 0, BackgroundVerts);
            GL.ColorPointer(4, All.UnsignedByte, 0, BackgroundColors);

            GL.EnableClientState(All.ColorArray);

            GL.DrawArrays(All.TriangleStrip, 0, BackgroundVerts.Length / 2);

            GL.DisableClientState(All.ColorArray);
			
			GL.Enable(All.DepthTest);
        }
    }
}
