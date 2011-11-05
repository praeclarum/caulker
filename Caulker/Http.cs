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
using System.IO;
using System.Net;

namespace Caulker
{
	public class Http
	{
		public static bool Download(string url, string dest)
		{
			//Console.WriteLine ("Downloading {0} to {1}", url, dest);
			
			int total = 0;
			
			try {
				using (var file = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.Read)) {	
					var req = GetRequest(url);
					using (var resp = req.GetResponse()) {
						using (var s = resp.GetResponseStream()) {							
							var buffer = new byte[4 * 1024];							
							var n = 1;
							while (n > 0) {
								n = s.Read(buffer, 0, buffer.Length);
								if (n > 0) {
									total += n;
									file.Write(buffer, 0, n);
								}
							}	
						}
					}
				}
				//Console.WriteLine ("Downloaded {0} KB for {1}", total/1024, url);
				return true;
			}
			catch (Exception ex) {
				Console.WriteLine ("! Download error: " + ex.Message + " " + url);
				try {
					File.Delete(dest);
				}
				catch(Exception) {
				}
				return false;
			}
		}
		
		static HttpWebRequest GetRequest(string url) {
			var r = (HttpWebRequest)WebRequest.Create(url);
			r.AllowAutoRedirect = true;
			r.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
			r.UserAgent = "WorldsFastest iPhone App";
			return r;
		}
	}
}
