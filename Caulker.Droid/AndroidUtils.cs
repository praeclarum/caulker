using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Drawing;
using Android.Graphics;
using Android;
using Android.Content.Res;

namespace Caulker
{
    /// <summary>
    /// Poor imitation of MonoTouch.UIKit.UITouch class
    /// </summary>
    public class UITouch
    {
        System.Drawing.PointF _prevLocation, _currLocation;
        public UITouch(float x, float y)
        {
            _currLocation = _prevLocation = new System.Drawing.PointF(x, y);
        }

        public void UpdateLocation(float x, float y)
        {
            _prevLocation = _currLocation;
            _currLocation = new System.Drawing.PointF(x, y);
        }

        public virtual System.Drawing.PointF LocationInView() 
        { 
            return _currLocation; 
        }

        public virtual System.Drawing.PointF PreviousLocationInView()
        {
            return _prevLocation;
        }
    }

    public class CaulkerUtils
    {
        internal static void SetMissingTile(Resources res, int resourceId)
        {
            MissingTile = BitmapFactory.DecodeResource(res, resourceId);
        }

        internal static Bitmap MissingTile
        {
            get;
            private set;
        }
    }
}