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
using OpenTK;
using OpenTK.Graphics.ES11;
using OpenTK.Platform.Android;
using System.Collections.Generic;
using Caulker;
using Android.Views;
using Android.Content;

namespace Caulker {

	public class WorldView : AndroidGameView 
    {
        uint _depthRenderbuffer;

		long _lastT = new DateTime().Ticks;		

		Background _background = new Background();

        List<IDrawable> _drawables = new List<IDrawable>();
        List<IDrawable> _drawablesNeedingLoad = new List<IDrawable>();
		
		public Camera Camera { get; private set; }
		public CameraMan CameraMan { get; set; }
		
		Location _sunLoc;
        public bool ShowSun { get; set; }


        public WorldView(Context context, int missingTileResId)
            : base(context)
        {
            // Manky hack to get the missing tile to be loadable by the class lib
            CaulkerUtils.SetMissingTile(context.Resources, missingTileResId);

            Initialize();
        }

	    void Initialize() {
			var now = DateTime.Now;
			_sunLoc = Caulker.Location.SunLocation(now);
	        Camera = new Camera();
			CameraMan = new BlimpCameraMan(
                new Location(153.0306, -27.4778),         // Greetings from Brisbane :)
                new Location(153.0306, -27.4878));
			Unload += HandleUnload;
	    }

        void HandleUnload(object sender, EventArgs e)
        {
            foreach (var d in _drawables)
            {
                d.StopDrawing();
            }
        }

        protected override void CreateFrameBuffer()
        {
            base.CreateFrameBuffer();

            //
            // Enable the depth buffer
            //
            var sz = Size;
            GL.Oes.GenRenderbuffers(1, ref _depthRenderbuffer);
            GL.Oes.BindRenderbuffer(All.RenderbufferOes, _depthRenderbuffer);
            GL.Oes.RenderbufferStorage(All.RenderbufferOes, All.DepthComponent16Oes, sz.Width, sz.Height);
            GL.Oes.FramebufferRenderbuffer(All.FramebufferOes, All.DepthAttachmentOes, All.RenderbufferOes, _depthRenderbuffer);
        }

        public void FreeMemory()
        {
            MakeCurrent();
            foreach (var d in _drawables)
            {
                d.FreeMemory();
            }
        }

        #region DisplayLink support - not needed in Android

        //int _frameInterval = 0;
        //CADisplayLink _displayLink;
        //bool _isRendering;

        //public void StartRendering()
        //{
        //    if (_isRendering)
        //        return;

        //    CreateFrameBuffer();
        //    CADisplayLink displayLink = UIScreen.MainScreen.CreateDisplayLink(this, new Selector("drawFrame"));
        //    displayLink.FrameInterval = _frameInterval;
        //    displayLink.AddToRunLoop(NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
        //    _displayLink = displayLink;

        //    _isRendering = true;
        //}

        //public void StopRendering()
        //{
        //    if (!_isRendering)
        //        return;
        //    _displayLink.Invalidate();
        //    _displayLink = null;
        //    DestroyFrameBuffer();
        //    _isRendering = false;
        //}

        ////[Export("drawFrame")]
        //void DrawFrame()
        //{
        //    OnRenderFrame(new FrameEventArgs());
        //}

        #endregion

        #region Drawing

        int frameCount = 0;

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            var sz = new Size((int)this.Width, (int)this.Height);
            if (sz.Width != Size.Width)
            {
                Size = sz;
            }

            MakeCurrent();

            //
            // Initialize drawables
            //
            foreach (var d in _drawablesNeedingLoad)
            {
                d.LoadContent();
            }
            _drawablesNeedingLoad.Clear();

            //
            // Determine the current sim time
            //
            var t = new DateTime().Ticks;
            var wallNow = DateTime.UtcNow;

            var time = new SimTime()
            {
                Time = wallNow,
                WallTime = wallNow,
                TimeElapsed = t - _lastT,
                WallTimeElapsed = t - _lastT
            };
            _lastT = t;

            //Console.WriteLine ("FPS {0:0}", 1.0 / time.WallTimeElapsed);

            GL.Viewport(0, 0, Size.Width, Size.Height);

            GL.ClearColor(158 / 255.0f, 207 / 255.0f, 237 / 255.0f, 1.0f);
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
            if (ShowSun)
            {
                GL.Enable(All.Lighting);
                GL.Enable(All.ColorMaterial);

                GL.Enable(All.Light0);
                var sp = _sunLoc.ToPositionAboveSeaLevel(150000000);
                GL.Light(All.Light0, All.Position, new float[] { sp.X, sp.Y, sp.Z, 1 });
            }

            //
            // Draw all the layers
            //
            foreach (var d in _drawables)
            {
                d.Draw(Camera, time);
            }

            if (ShowSun)
            {
                GL.Disable(All.Lighting);
                GL.Disable(All.ColorMaterial);
            }

            SwapBuffers();

            frameCount++;
        }

        public void AddDrawable(IDrawable drawable)
        {
            _drawables.Add(drawable);
            _drawablesNeedingLoad.Add(drawable);
        }
        #endregion

        #region Gesture recognition

        enum GestureType
        {
            None,
            Panning,
            Pitching,
            RotatingScaling,
        }
        GestureType _gesture = GestureType.None;

        ManualCameraMan ForceManualCameraMan()
        {
            var man = CameraMan as ManualCameraMan;
            if (man == null)
            {
                man = new ManualCameraMan(CameraMan.Pos3d.ToLocation(), CameraMan.LookAt);
                CameraMan = man;
            }
            return man;
        }
		
        public override bool OnTouchEvent(Android.Views.MotionEvent e)
        {
            base.OnTouchEvent(e);

            // Dump evt
            dumpEvent(e);

            switch (e.Action) 
            {
                case MotionEventActions.Down:
                case MotionEventActions.PointerDown:
                    TouchesBegan(e);
                    break;

                case MotionEventActions.Up:
                case MotionEventActions.PointerUp:
                    TouchesEnded(e);
                    break;

                case MotionEventActions.Move:
                    TouchesMoved(e);
                    break;
            }
           
            return true;
        }

        private double Spacing(MotionEvent evt) 
        {
            float x = evt.GetX(0) - evt.GetX(1);
            float y = evt.GetY(0) - evt.GetY(1);
            return Math.Sqrt(x * x + y * y);
        }

        Dictionary<int, UITouch> _activeTouches = new Dictionary<int, UITouch>();

		public void TouchesBegan (MotionEvent evt)
		{
            for (int i = 0; i < evt.PointerCount; i++)
            {
                if (_activeTouches.ContainsKey(i))
                    _activeTouches[i].UpdateLocation(evt.GetX(i), evt.GetY(i));
                else
                    _activeTouches.Add(i, new UITouch(evt.GetX(i), evt.GetY(i)));
            }
		}

		public void TouchesEnded (MotionEvent evt)
		{
            _activeTouches.Clear();

			if (_activeTouches.Count == 0) {
				_gesture = GestureType.None;
			}
		}

		public void TouchesMoved (MotionEvent evt)
		{
            Console.WriteLine (_gesture + ": ActiveTouches: "+_activeTouches.Count);
			//var ts = touches.ToArray<UITouch>();

            TouchesBegan(evt);

			// Make sure we have a camera man that can take orders
			var man = ForceManualCameraMan();
			
			var ts = _activeTouches.Values.ToArray();
	
			if (_activeTouches.Count == 1) {
				var t = ts[0];
				var start = Camera.GetLocation(t.PreviousLocationInView());
				var end = Camera.GetLocation(t.LocationInView());
				if (start != null && end != null) {
					man.Drag(start, end);
				}
			}
			else if (_activeTouches.Count == 2) {
				var ppt0 = ts[0].PreviousLocationInView();
				var ppt1 = ts[1].PreviousLocationInView();
				
				var pt0 = ts[0].LocationInView();
				var pt1 = ts[1].LocationInView();

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

        /** Show an event in the LogCat view, for debugging */
        private void dumpEvent(MotionEvent evt) {
            String[] names = { "DOWN" , "UP" , "MOVE" , "CANCEL" , "OUTSIDE" ,
                "POINTER_DOWN" , "POINTER_UP" , "7?" , "8?" , "9?" };

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int action = (int)evt.Action;
            int actionCode = action & (int)MotionEventActions.Mask;
            sb.Append("event ACTION_" ).Append(names[actionCode]);
            if (actionCode == (int)MotionEventActions.PointerDown
                    || actionCode == (int)MotionEventActions.PointerUp) {
                sb.Append("(pid " ).Append(
                action >> (int)MotionEventActions.PointerIdShift);
                sb.Append(")" );
            }
            sb.Append("[" );
            for (int i = 0; i < evt.PointerCount; i++) {
                sb.Append("#" ).Append(i);
                sb.Append("(pid " ).Append(evt.GetPointerId(i));
                sb.Append(")=" ).Append((int) evt.GetX(i));
                sb.Append("," ).Append((int) evt.GetY(i));
                if (i + 1 < evt.PointerCount)
                    sb.Append(";" );
            }
            sb.Append("] - ActionIndex: " + evt.ActionIndex + ", ActionIndexPointerId: " + evt.GetPointerId(evt.ActionIndex) );

            Console.WriteLine(sb.ToString());
        }
    }

    public class Geometry
    {
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
        public Background()
        {
            for (var i = 1; i < BackgroundVerts.Length; i += 2)
            {
                BackgroundVerts[i] = BackgroundVerts[i] / 2 + 0.5f;
            }
        }
        public void Render()
        {

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
