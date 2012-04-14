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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK.Graphics.ES11;
using OpenTK;
using System.Drawing;

namespace Caulker
{
	public interface CameraMan
	{
		Location LookAt { get; }
		Vector3d LookAt3d { get; }
		Vector3d Pos3d { get; }
		
		bool Dirty { get; set; }
		
		void SetLookAt (ILocatable lookAt, bool animated);
		void Update (SimTime t);
	}
	
	public class ManualCameraMan : CameraMan
	{
		public Location LookAt { get; private set; }
		public Vector3d LookAt3d { get; private set; }
		public Vector3d Pos3d { get; private set; }
		
		public bool Dirty { get; set; }

		Location _pos;
		float _altitude = 0.3f;

		public ManualCameraMan (ILocatable posLoc, ILocatable lookAt)
		{
			Dirty = true;
			_pos = new Location(posLoc);
			Pos3d = _pos.ToPositionAboveSeaLeveld (_altitude);
			LookAt = new Location (lookAt);
			LookAt3d = LookAt.ToPositionAboveSeaLeveld (0);
		}

		public void Update (SimTime t)
		{
		}

		public void SetLookAt (ILocatable lookAt, bool animated)
		{
		}

		public void Drag (Location start, Location end)
		{
			var d = -start.VectorTo(end);
			
			_pos = _pos.LocationAway(d);
			Pos3d = _pos.ToPositionAboveSeaLeveld (_altitude);
			LookAt = LookAt.LocationAway(d);
			LookAt3d = LookAt.ToPositionAboveSeaLeveld (0);
			
			Dirty = true;
		}

		public void Pitch (float d)
		{
			//throw new System.NotImplementedException ();
		}

		public void Scale (ILocatable loc, float scale)
		{
			var toCam = (Pos3d - LookAt3d); 
			var dist = toCam.Length;
			
			var sdist = dist / scale;
			toCam.Normalize();
			var newPos = LookAt3d + toCam * sdist;
			
			var alt = newPos.Length - Location.EarthRadiusInKm;
			
			_altitude = (float)alt;
			if (_altitude > 12500.0f) _altitude = 12500.0f;
			if (_altitude < 0.03f) _altitude = 0.03f;

			Pos3d = _pos.ToPositionAboveSeaLeveld(_altitude);
			
			Dirty = true;
		}

		public void Rotate (ILocatable loc, float ddegs)
		{
			_pos = _pos.RotateAround(loc, ddegs);
			LookAt = LookAt.RotateAround(loc, ddegs);
			
			Pos3d = _pos.ToPositionAboveSeaLeveld (_altitude);
			LookAt3d = LookAt.ToPositionAboveSeaLeveld (0);
			
			Dirty = true;
		}
	}

	public class BlimpCameraMan : CameraMan
	{
		public Location LookAt { get; private set; }
		public Vector3d LookAt3d { get; private set; }
		public Vector3d Pos3d { get; private set; }

		LocLinAnim _lookAtAnim;
		VectorLinAnim _posAnim;

		float _altitude = 0.3f;

		public bool Dirty { get; set; }

		public BlimpCameraMan (ILocatable posLoc, ILocatable lookAt)
		{
			Dirty = true;
			Pos3d = posLoc.ToPositionAboveSeaLeveld (_altitude);
			LookAt = new Location (lookAt);
			LookAt3d = LookAt.ToPositionAboveSeaLeveld (0);
		}

		public void Update (SimTime t)
		{
			if (_lookAtAnim != null) {
				if (_lookAtAnim.Update (t)) {
					LookAt = _lookAtAnim.Value;
					LookAt3d = LookAt.ToPositionAboveSeaLeveld (0);
					Dirty = true;
				} else {
					_lookAtAnim = null;
				}
			}
			if (_posAnim != null) {
				if (_posAnim.Update (t)) {
					Pos3d = _posAnim.Value;
					Dirty = true;
				} else {
					_posAnim = null;
				}
			}
		}

		public void SetLookAt (ILocatable lookAt, bool animated)
		{
			if (animated) {
				_lookAtAnim = new LocLinAnim(2, LookAt, lookAt);
				var oldLocPos = LookAt.ToPositionAboveSeaLeveld(_altitude);
				_posAnim = new VectorLinAnim(5, Pos3d, oldLocPos);
			}
			else {
				LookAt = new Location(lookAt);
				LookAt3d = LookAt.ToPositionAboveSeaLeveld (0);
				Dirty = true;
			}
		}
	}

	public class Camera
	{
		Matrix4 _projectionMatrixF;
		Matrix4 _viewMatrixF;
		Matrix4d _projectionMatrix;
		Matrix4d _viewMatrix;

		float _aspect = 320f / 480f;
		int Width = 320, Height = 480;

		
		const double LandThickness = 10000;
		const double Nearest = 0.01;
		
		bool _dirty = true;
		
		public float UnitsPerPixel { get; private set; }
		public double MetersPerPixel { get; private set; }
		public double RadiansPerPixel { get; private set; }
		public int Zoom { get; private set; }
		public Location LookAt { get; private set; }
		
		public Location[] ScreenLocations { get; private set; }

		public Camera ()
		{
			_projectionMatrixF = Matrix4.Identity;
			_viewMatrixF = Matrix4.Identity;
			_projectionMatrix = Matrix4d.Identity;
			_viewMatrix = Matrix4d.Identity;
			ScreenLocations = new Location[21];
		}
		
		public void SetViewport(int width, int height) {
			if (Width != width || Height != height) {
				Width = width;
				Height = height;
				_aspect = (float)Width / (float)Height;
			}
		}

		public void Execute (CameraMan man)
		{
			if (man.Dirty || _dirty) {			
				
				LookAt = man.LookAt;
				
				//
				// Distance from 
				//
				var distanceToLookAt = (man.Pos3d - man.LookAt3d).Length;
				var near = distanceToLookAt - LandThickness/2;
				if (near < Nearest) {
					near = Nearest;
				}
				var far = near + LandThickness;
				
				//
				// Regenerate the matrices
				//
				_projectionMatrix = Matrix4d.CreatePerspectiveFieldOfView (MathHelper.DegreesToRadians (45), _aspect, near, far);
				_projectionMatrixF = ToMatrix4 (_projectionMatrix);				
				var pos = man.Pos3d;
				var lookAt = man.LookAt3d;				
				var _up = pos;
				_up.Normalize ();				
				_viewMatrix = Matrix4d.LookAt (pos, lookAt, _up);				
				_viewMatrixF = ToMatrix4 (_viewMatrix);
				
				//
				// Calculate some spots on the screen so we know what's visible
				//				
				ScreenLocations[0] = GetLocation(new PointF(Width, 0));
				ScreenLocations[1] = GetLocation(new PointF(Width, Height/8));
				ScreenLocations[2] = GetLocation(new PointF(Width, Height/3));
				ScreenLocations[3] = GetLocation(new PointF(Width, Height/2));
				ScreenLocations[4] = GetLocation(new PointF(Width, Height*7/8));				
				ScreenLocations[5] = GetLocation(new PointF(Width, Height*2/3));
				ScreenLocations[6] = GetLocation(new PointF(Width, Height));
				
				ScreenLocations[7] = GetLocation(new PointF(0, 0));
				ScreenLocations[8] = GetLocation(new PointF(0, Height/8));
				ScreenLocations[9] = GetLocation(new PointF(0, Height/3));
				ScreenLocations[10] = GetLocation(new PointF(0, Height/2));
				ScreenLocations[11] = GetLocation(new PointF(0, Height*7/8));				
				ScreenLocations[12] = GetLocation(new PointF(0, Height*2/3));
				ScreenLocations[13] = GetLocation(new PointF(0, Height));
				
				ScreenLocations[14] = GetLocation(new PointF(Width/2, 0));
				ScreenLocations[15] = GetLocation(new PointF(Width/2, Height/8));
				ScreenLocations[16] = GetLocation(new PointF(Width/2, Height/3));
				ScreenLocations[17] = GetLocation(new PointF(Width/2, Height/2));
				ScreenLocations[18] = GetLocation(new PointF(Width/2, Height*7/8));				
				ScreenLocations[19] = GetLocation(new PointF(Width/2, Height*2/3));
				ScreenLocations[20] = GetLocation(new PointF(Width/2, Height));
				
				//
				// Calculate the rads per pixel X
				//
				var dp = 100;
				var left = new PointF(Width/2 - dp/2, 15*Height/24);
				var right = new PointF(Width/2 + dp/2, 15*Height/24);
				var leftLoc = GetLocation(left);
				var rightLoc = GetLocation(right);
				var leftPos = GetPosition(left);
				var rightPos = GetPosition(right);
				if (leftLoc != null && rightLoc != null) {
					var ddegs = rightLoc.VectorTo(leftLoc).Length;
					RadiansPerPixel = ddegs / dp * Math.PI / 180;
					
					var dkm = (rightPos.Value - leftPos.Value).Length;
					var kmPerPixel = dkm / dp;
					UnitsPerPixel = (float)(kmPerPixel);
					MetersPerPixel = kmPerPixel * 1000;
					
					//
					// Calculate the zoom level
					//
					var IdealZoom = Math.Log(2*Math.PI / 256 / RadiansPerPixel) / Math.Log(2);
					Zoom = (int)(IdealZoom + 0.5); // Rounded to nearest
					if (Zoom > 18) Zoom = 18;
					else if (Zoom < 4) Zoom = 4;
										
					//Console.WriteLine ("rpp = {0}; mpp = {1}; zoom = {2}", RadiansPerPixel, MetersPerPixel, IdealZoom);
				}
				
				_dirty = false;
				man.Dirty = false;
			}
			
			GL.MatrixMode (All.Projection);
			GL.LoadMatrix (ref _projectionMatrixF.Row0.X);
			GL.MatrixMode (All.Modelview);
			GL.LoadMatrix (ref _viewMatrixF.Row0.X);
		}

		static Matrix4 ToMatrix4 (Matrix4d m)
		{
			var o = new Matrix4 ();
			o.Row0 = m.Row0.ToVector4 ();
			o.Row1 = m.Row1.ToVector4 ();
			o.Row2 = m.Row2.ToVector4 ();
			o.Row3 = m.Row3.ToVector4 ();
			return o;
		}
		
		bool TrySphereIntersect(Vector3d p1, Vector3d p2,  double r, out Vector3d p) {
			var d21 = p2 - p1;
			
			var a = d21.X*d21.X + d21.Y*d21.Y + d21.Z*d21.Z;
			var b = 2*(d21.X*p1.X + d21.Y*p1.Y + d21.Z*p1.Z);
			var c = p1.X*p1.X + p1.Y*p1.Y + p1.Z*p1.Z - r*r;
			
			var e = b*b - 4*a*c;
			
			if (e <= 0) {
				p = Vector3d.Zero;
				return false;
			}
			
			var up = (-b + Math.Sqrt(e)) / (2*a);
			var um = (-b - Math.Sqrt(e)) / (2*a);
			var u = 0.0;
			if (0.0 < up && up < 1.0) {
				u = up;
			}
			else {
				u = um;
			}
			
			p = new Vector3d((p1.X + u * d21.X),
			                 (p1.Y + u * d21.Y),
			                 (p1.Z + u * d21.Z));
			return true;
		}
		
		Vector3d? GetPosition (PointF pt) {
			var p1 = GetLocation(pt,  1);
			var p2 = GetLocation(pt, -1);
			Vector3d p;
			if (TrySphereIntersect(p1, p2, Location.EarthRadiusInKm, out p)) {			
				return p;
			}
			else {
				return null;
			}
		}
		
		public Location GetLocation (PointF pt) {
			var p = GetPosition(pt);
			if (p.HasValue) {
				return p.Value.ToLocation();
			}
			else {
				return null;
			}
		}
		
		Vector3d GetLocation (PointF pt, float z)
		{
			var w2 = Width / 2.0;
			var h2 = Height / 2.0;
			var screen = new Vector4d ((pt.X - w2) / w2, (h2 - pt.Y) / h2, z, 1);
			
			var final = Matrix4d.Mult (_viewMatrix, _projectionMatrix);
			final.Invert ();
			
			var pos = final.Tx (screen);
			
			var p = new Vector3d (pos.X / pos.W, pos.Y / pos.W, pos.Z / pos.W);
			
			//Console.WriteLine ("{0} -> {1}", pt, p);
			
			return p;
		}

		
	}

	public class VectorLinAnim
	{
		double _accTime;
		double _duration;

		Vector3d _start, _end, _current;

		public Vector3d Value {
			get { return _current; }
		}

		public VectorLinAnim (double duration, Vector3d start, Vector3d end)
		{
			_start = start;
			_end = end;
			_current = _start;
			_duration = duration;
		}

		public bool Update (SimTime t)
		{
			if (_accTime > _duration) {
				return false;
			} else {
				_accTime += t.WallTimeElapsed;
				var a = _accTime / _duration;
				if (a > 1)
					a = 1;
				//a *= a;
				//a = 1 - a;
				_current = _start + (_end - _start) * a;
				return true;
			}
		}
	}

	public class LocLinAnim
	{
		double _accTime;
		double _duration;

		Location _start, _end, _current;

		public Location Value {
			get { return _current; }
		}

		public LocLinAnim (double duration, ILocatable start, ILocatable end)
		{
			_start = new Location(start);
			_end = new Location(end);
			_current = _start;
			_duration = duration;
		}

		public bool Update (SimTime t)
		{
			if (_accTime > _duration) {
				return false;
			} else {
				_accTime += t.WallTimeElapsed;
				var a = _accTime / _duration;
				if (a > 1)
					a = 1;
				a *= a;
				//a = 1 - a;
				_current = _start.LocationAway (_start.VectorTo (_end) * (float)a);
				return true;
			}
		}
	}

	public class VectorPidAnim
	{
		public double Proportional { get; set; }
		public double Integral { get; set; }
		public double Differential { get; set; }

		Vector3d _ierr = Vector3d.Zero;
		Vector3d _lastError = Vector3d.Zero;

		Vector3d _start, _end, _current;

		public Vector3d Value {
			get { return _current; }
		}

		public VectorPidAnim (double p, double i, double d, Vector3d start, Vector3d end)
		{
			_start = start;
			_end = end;
			_current = _start;
			Proportional = 0.5;
			Differential = 0;
			Integral = 0;
		}

		public bool Update (SimTime t)
		{
			var dt = t.WallTimeElapsed;
			
			var err = _end - _current;
			
			var derr = (err - _lastError) / dt;
			
			_ierr += err * dt;
			
			var p = Proportional * dt;
			var i = Integral * dt;
			var d = Differential * dt;
			
			var change = err * p + derr * d + _ierr * i;
			
			if (err.Length > 1E-05) {
				_current += change;
				_lastError = err;
				return true;
			} else {
				return false;
			}
		}
	}	
}

