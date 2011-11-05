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
using OpenTK;
using System.Drawing;

namespace Caulker
{
	public interface ILocatable {
		double Longitude { get; }
		double Latitude { get; }
	}
	
	public enum OrdinalDirection {
		N = 0, NE = 45, E = 90, SE = 135, S = 180, SW = 225, W = 270, NW = 315
	}
	
	public static class LocatableEx {
		
		public static PointF ToMercator(this ILocatable a) {
			var latRads = a.Latitude * Math.PI / 180;
			return new PointF((float)a.Longitude,
			                  (float)((Math.Log(Math.Tan(latRads) + (1.0 / Math.Cos(latRads))))*180/Math.PI));
		}
		
		public static Vector3d ToVector3d(this Vector3 v) {
			return new Vector3d(v.X, v.Y, v.Z);
		}
		public static Vector4 ToVector4(this Vector4d v) {
			return new Vector4((float)v.X, (float)v.Y, (float)v.Z, (float)v.W);
		}
		public static Vector2 VectorTo(this ILocatable fr, ILocatable to) {
			return new Vector2((float)(to.Longitude - fr.Longitude),
			                   (float)(to.Latitude - fr.Latitude));
		}
		public static Vector2 VectorTo(this PointF fr, PointF to) {
			return new Vector2((float)(to.X - fr.X),
			                   (float)(to.Y - fr.Y));
		}
		public static Location LocationAway(this ILocatable fr, Vector2 d) {
			return new Location(fr.Longitude + d.X, fr.Latitude + d.Y);
		}
		
		public static Location ToLocationSimple(this Vector3d v) {
			return new Location(v.X, v.Y);
		}
        public static Vector3 ToPositionAboveSeaLevelSimple(this ILocatable loc, float kmAboveSea) {
			var p = loc.ToMercator();
			return new Vector3(p.X, p.Y, kmAboveSea*0.000156785581f);
		}
		
		public static Location ToLocation(this Vector3d v) {
			var r = Math.Sqrt(v.X*v.X + v.Y*v.Y + v.Z*v.Z);
			var theta = Math.Atan2(v.Y, v.X);
			var phi = Math.Acos(v.Z / r);
			var lat = 90 - (180.0/Math.PI)*phi;
			var lon = (180.0/Math.PI)*theta;
			return new Location(lon, lat);
		}
		public static Vector3d ToPositionAboveSeaLeveld(this ILocatable loc, double kmAboveSea) {
	        var lon = loc.Longitude;
	        var lat = loc.Latitude;
			var omega = (Math.PI/180.0) * lon;
			var phi = (Math.PI/180.0) * lat;
			var r = (Location.EarthRadiusInKm + kmAboveSea);
		
			Vector3d v;
			v.X = (r * Math.Cos(phi) * Math.Cos(omega));
			v.Y = (r * Math.Cos(phi) * Math.Sin(omega));
			v.Z = (r * Math.Sin(phi));
			return v;
		}
		public static Vector3 ToPositionAboveSeaLevel(this ILocatable loc, float kmAboveSea) {
	        var lon = loc.Longitude;
	        var lat = loc.Latitude;
			var omega = (Math.PI/180.0) * lon;
			var phi = (Math.PI/180.0) * lat;
			var r = (Location.EarthRadiusInKm + kmAboveSea);
		
			Vector3 v;
			v.X = (float)(r * Math.Cos(phi) * Math.Cos(omega));
			v.Y = (float)(r * Math.Cos(phi) * Math.Sin(omega));
			v.Z = (float)(r * Math.Sin(phi));
			return v;
		}
		
        public static double MilesTo	(this ILocatable start, ILocatable finish) {
            return Location.EarthRadiusInMiles * ArcTo(start, finish);
        }
		
		public static double FeetTo(this ILocatable start, ILocatable finish) {
            return 5280 * MilesTo(start, finish);
        }

        public static double KmTo(this ILocatable start, ILocatable finish) {
            return Location.EarthRadiusInKm * ArcTo(start, finish);
        }

		static double ArcTo(this ILocatable start, ILocatable finish) {
			
			var lon1 = start.Longitude;
			var lat1 = start.Latitude;
			var lon2 = finish.Longitude;
			var lat2 = finish.Latitude;
			
			
			var dlon = ToRadians(lon2 - lon1);
			var dlat = ToRadians(lat2 - lat1);
			
			var clat1 = Math.Cos(ToRadians(lat1));
			var clat2 = Math.Cos(ToRadians(lat2));
			
			var sdlat2 = Math.Sin(dlat/2);
			var sdlon2 = Math.Sin(dlon/2);
			
			var a = (sdlat2*sdlat2) + clat1 * clat2 * (sdlon2*sdlon2);
			var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));

            return c;
		}
		
		static double ToRadians(double degs) {
			return Math.PI * degs / 180;
		}
		
		public static Location RotateAround(this ILocatable loc, ILocatable axis, float degs) {			
			var a = degs * Math.PI / 180;
			var p = loc.ToPositionAboveSeaLeveld(0);
			var o = axis.ToPositionAboveSeaLeveld(0);
			o.Normalize();
			
			var m = Matrix4d.CreateFromAxisAngle(o, a);
			
			var r = m.Tx(p);
			
			return r.ToLocation();
		}
		
		public static Vector3d Tx (this Matrix4d m, Vector3d v)
		{
			var vv = new Vector4d(v.X, v.Y, v.Z, 1);
			var rr = new Vector4d (Vector4d.Dot (m.Column0, vv), Vector4d.Dot (m.Column1, vv), Vector4d.Dot (m.Column2, vv), Vector4d.Dot (m.Column3, vv));
			return new Vector3d(rr.X, rr.Y, rr.Z);
		}
		
		public static Vector4d Tx (this Matrix4d m, Vector4d v)
		{
			return new Vector4d (Vector4d.Dot (m.Column0, v), Vector4d.Dot (m.Column1, v), Vector4d.Dot (m.Column2, v), Vector4d.Dot (m.Column3, v));
		}
		
		public static Vector4 Tx (this Matrix4 m, Vector4 v)
		{
			return new Vector4 (Vector4.Dot (m.Column0, v), Vector4.Dot (m.Column1, v), Vector4.Dot (m.Column2, v), Vector4.Dot (m.Column3, v));
		}
		
		public static double HeadingTo(this ILocatable start, ILocatable finish) {
			
			var n = new Location(
				start.Longitude,
				finish.Latitude
			);
			var e = new Location(
				finish.Longitude,
				start.Latitude
			);
			var northMiles = start.MilesTo(n);
			if (n.Latitude < start.Latitude) {
				northMiles *= -1;
			}
			var eastMiles = start.MilesTo(e);
			if (e.Longitude < start.Longitude) {
				eastMiles *= -1;
			}
			
			var a = Math.Atan2(northMiles, eastMiles);
			
			if (a < 0) {
				a += Math.PI * 2;
			}
			
			var heading = 90 - a*180/Math.PI;
			
			while (heading < 0) {
				heading += 360;
			}
			while (heading >= 360) {
				heading -= 360;
			}
			
			return heading;
		}
		public static OrdinalDirection DirTo(this ILocatable start, ILocatable finish) {
			var h = HeadingTo(start, finish);
			
			var i = (((int)(h + 45/2)) / 45) * 45;
			
			if (i >= 360) {
				i -= 360;
			}
			
			return (OrdinalDirection)i;
		}
        public static string ToLocationString(this ILocatable loc, string format) {
            var lonDir = loc.Longitude < 0 ? "W" : "E";
            var latDir = loc.Latitude < 0 ? "S" : "N";
            return string.Format(format,
                string.Format("{0:0.####}\xB0{1}", Math.Abs(loc.Latitude), latDir),
                string.Format("{0:0.####}\xB0{1}", Math.Abs(loc.Longitude), lonDir));
        }
		public static string ToLocationString(this ILocatable loc) {
            return ToLocationString(loc, "(latitude {0}, longitude {1})");
        }
	}
	
	public class Location : ILocatable {
		public Location(ILocatable loc) {
			Longitude = loc.Longitude;
			Latitude = loc.Latitude;
		}
		public Location(double lon, double lat) {
			Longitude = lon;
			Latitude = lat;
		}
		public double Longitude { get; set; }
		public double Latitude { get; set; }
		
		public const double EarthRadiusInKm = 6378.137;
		public const double EarthRadiusInMiles = 3963.19059;
		
		public static readonly Location NorthPole = new Location(0.0, 90.0);
		public static readonly Location SouthPole = new Location(0.0, -90.0);
		public static readonly Location NorthMagneticPole = new Location(-133.0, 85.1);
		public static readonly Location SouthMagneticPole = new Location(137.4, -64.4);
		
		public override string ToString ()
		{
            return this.ToLocationString();
		}
		
		static double solar_declination(DateTime t) {
			/* day angle : */
			double J = 1.0 + ((double) t.DayOfYear);
			double tau_d = 2.0 * Math.PI * (J - 1.0) / 365.0;
			
			/* solar declination */
			double delta_s = 0.006981 - 0.399912 * Math.Cos(      tau_d) + 0.070257 * Math.Sin(      tau_d)
			- 0.006758 * Math.Cos(2.0 * tau_d) + 0.000907 * Math.Sin(2.0 * tau_d)
			- 0.002697 * Math.Cos(3.0 * tau_d) + 0.001480 * Math.Sin(3.0 * tau_d);
			
			return delta_s;
		}
		public static Location SunLocation(DateTime time) {
			double hour = time.Hour;
			
			var latitude = solar_declination(time)*180/Math.PI;
			var longitude = (Math.PI - (2.0 * Math.PI * hour / 24.0))*180/Math.PI;
			
//			printf("latitude  : %10.5f radians (%10.5f degrees)\n", latitude, TO_DEGS(latitude));
//			printf("longitude : %10.5f radians (%10.5f degrees)\n", longitude, TO_DEGS(longitude));
			
			return new Location(longitude, latitude);
		}
	}
	
	
	public interface IAddressable {
		string Thoroughfare { get; }
		string SubThoroughfare { get; }
		string Locality { get; }
		string SubLocality { get; }
		string AdministrativeArea { get; }
		string SubAdministrativeArea { get; }
		string PostalCode { get; }
		string CountryCode { get; }
	}
	
	public class Address : IAddressable {

		public string Thoroughfare { get; set; }
		public string SubThoroughfare { get; set; }
		public string Locality { get; set; }
		public string SubLocality { get; set; }
		public string AdministrativeArea { get; set; }
		public string SubAdministrativeArea { get; set; }
		public string PostalCode { get; set; }
		public string CountryCode { get; set; }

		public override string ToString ()
		{
			return string.Format("[Address: SubThoroughfare={0}, Thoroughfare={1}, Locality={2}, SubLocality={3}, AdministrativeArea={4}, SubAdministrativeArea={5}, PostalCode={6}, CountryCode={7}]", SubThoroughfare, Thoroughfare, Locality, SubLocality, AdministrativeArea, SubAdministrativeArea, PostalCode, CountryCode);
		}
	}
	
	public static class AddressableEx {
		
		public static string InlineAddressString(this IAddressable addr) {
			return	addr.SubThoroughfare + " " +
					addr.Thoroughfare + ", " +
					addr.Locality + ", " +
					addr.AdministrativeArea + ", " +
					addr.CountryCode;
		}
		
	}
	
	public interface IBoundingBox
	{
		double MinLon { get; }
		double MaxLon { get; }
		double MinLat { get; }
		double MaxLat { get; }
	}
	
	
	public class BoundingBox : IBoundingBox {
		
		public double MinLon { get; private set; }
		public double MaxLon { get; private set; }
		public double MinLat { get; private set; }
		public double MaxLat { get; private set; }
				
		public double Width { get { return (MaxLon - MinLon); } }
		public double Height { get { return (MaxLat - MinLat); } }
		
		public BoundingBox(double minLon, double minLat, double maxLon, double maxLat) {
			MinLon = minLon;
			MaxLon = maxLon;
			MinLat = minLat;
			MaxLat = maxLat;
		}
		
		public BoundingBox(ILocatable loc, double radiusInMiles) {
			var lon = loc.Longitude;
			var lat = loc.Latitude;
			
			var milesPerLon = loc.MilesTo(new Location(lon + 1, lat));
			var milesPerLat = loc.MilesTo(new Location(lon, lat + 1));
			
			var dlon = radiusInMiles / milesPerLon;
			var dlat = radiusInMiles / milesPerLat;
			
			MinLon = lon - dlon;
			MaxLon = lon + dlon;
			MinLat = lat - dlat;
			MaxLat = lat + dlat;
		}
		
		public BoundingBox(params ILocatable[] includedLocs) {
			
			double minLon=0, maxLon=0;
			double minLat=0, maxLat=0;
			
			for (var i = 0; i < includedLocs.Length; i++) {
				var loc = includedLocs[i];
				
				if (i == 0) {
					minLon = maxLon = loc.Longitude;
					minLat = maxLat = loc.Latitude;
				}
				else {
					minLon = Math.Min(minLon, loc.Longitude);
					maxLon = Math.Max(maxLon, loc.Longitude);
					minLat = Math.Min(minLat, loc.Latitude);
					maxLat = Math.Max(maxLat, loc.Latitude);
				}
			}
			
			MinLat = minLat;
			MinLon = minLon;
			MaxLat = maxLat;
			MaxLon = maxLon;
		}

        public BoundingBox Expand(double dlon, double dlat) {
            var lon = dlon / 2;
            var lat = dlat / 2;
            return new BoundingBox() {
                MinLon = MinLon - lon,
                MaxLon = MaxLon + lon,
                MinLat = MinLat - lat,
                MaxLat = MaxLat + lat
            };
        }

        public Location Center {
            get {
                return new Location(
                    (MinLon + MaxLon) / 2,
                    (MinLat + MaxLat) / 2
                );
            }
        }
		
		public Location[] EdgeLocations() {
			return new Location[] {
				North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest
			};
		}
		
		public Location North { get { return new Location(MinLon+Width/2, MaxLat); } }
		public Location NorthEast { get { return new Location(MaxLon, MaxLat); } }
		public Location East { get { return new Location(MaxLon, MinLat+Height/2); } }
		public Location SouthEast { get { return new Location(MaxLon, MinLat); } }
		public Location South { get { return new Location(MinLon+Width/2, MinLat); } }
		public Location SouthWest { get { return new Location(MinLon, MinLat); } }
		public Location West { get { return new Location(MinLon, MinLat+Height/2); } }
		public Location NorthWest { get { return new Location(MinLon, MaxLat); } }
		
		public override string ToString ()
		{
			return string.Format("[BB: MinLon={0}, MaxLon={1}, MinLat={2}, MaxLat={3}]", MinLon, MaxLon, MinLat, MaxLat);
		}

	}

}
