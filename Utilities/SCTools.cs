using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using SaveChem.Models;
using Newtonsoft.Json.Linq;

namespace SaveChem.Utilities
{
	public enum SCDataMode { OBJ, JSON, ZIP, BASE };

	public class SCTools
	{
		#region compression

		/// <summary>
		/// Main compression routine. Can forward-convert through SCDataMode
		/// </summary>
		/// <param name="src"></param>
		/// <param name="srcType"></param>
		/// <param name="dstType"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public static dynamic Compress(dynamic src,
			SCDataMode srcType = SCDataMode.OBJ,
			SCDataMode dstType = SCDataMode.BASE,
			DateTime? time = null)
		{
			if (srcType == dstType)
				return src;

			dynamic dst = src;

			// --- Actual conversions ---
			switch (srcType)
			{
				// in: object, out: string
				case SCDataMode.OBJ:
					dst = JsonConvert.SerializeObject(dst);
					if (dstType == SCDataMode.JSON)
						break;
					goto case SCDataMode.JSON;

				// in: string, out: byte[]
				case SCDataMode.JSON:
					byte[] data = Encoding.UTF8.GetBytes(dst);

					using (MemoryStream sout = new MemoryStream())
					{
						using (GZipStream zipper = new GZipStream(sout, CompressionMode.Compress))
						{
							zipper.Write(data, 0, data.Length);
						}
						dst = sout.ToArray();
					}

					// Add timestamp to things.
					if (time.HasValue)
					{
						DateTime nix = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
						Int32 stamp = (Int32)(time.Value - nix).TotalSeconds;
						data = dst as byte[];
						for (int i = 0; i < 4; i++)
							data[4 + i] = Convert.ToByte(stamp >> (i * 8) & 0xFF);
					}

					if (dstType == SCDataMode.ZIP)
						break;
					goto case SCDataMode.ZIP;

				// in: byte[], out: string
				case SCDataMode.ZIP:
					dst = Convert.ToBase64String(dst,Base64FormattingOptions.InsertLineBreaks);
					break;
				default:
					//# TODO : fail safes and such.
					break;
			}

			return dst;
		}

		/// <summary>
		/// Main decompression routine. Can backward-convert through SCDataMode
		/// </summary>
		/// <param name="src"></param>
		/// <param name="srcType"></param>
		/// <param name="dstType"></param>
		/// <returns></returns>
		public static dynamic Decompress(dynamic src,
			SCDataMode srcType = SCDataMode.BASE,
			SCDataMode dstType = SCDataMode.OBJ)
		{
			if (srcType == dstType)
				return src;

			dynamic dst = src;

			try
			{
				// --- Actual conversions ---
				switch (srcType)
				{
					// in: string, out: byte[]
					case SCDataMode.BASE:
						dst = Convert.FromBase64String(dst);
						if (dstType == SCDataMode.ZIP)
							break;
						goto case SCDataMode.ZIP;

					// in: byte[], out: string
					case SCDataMode.ZIP:
						using (MemoryStream sin = new MemoryStream(dst))
						using (MemoryStream sout = new MemoryStream())
						{
							using (GZipStream zipper = new GZipStream(sin, CompressionMode.Decompress))
							{
								zipper.CopyTo(sout);
							}
							dst = Encoding.UTF8.GetString(sout.ToArray());
						}
						if (dstType == SCDataMode.JSON)
							break;
						goto case SCDataMode.JSON;

					// in: string, out: object
					case SCDataMode.JSON:
						dst = JsonConvert.DeserializeObject(dst);
						break;
					default:
						//# TODO : fail safes and such.
						break;
				}
			}
			catch (Exception)
			{
				// Aww, nuts.
				return null;
			}
			return dst;
		}

		public static string CompressFull(dynamic obj)
		{
			return SCTools.Compress(obj, SCDataMode.OBJ, SCDataMode.BASE, DateTime.UtcNow);
		}

		public static dynamic DecompressFull(string src)
		{
			return SCTools.Decompress(src, SCDataMode.BASE, SCDataMode.OBJ);
		}

		#endregion

		/// <summary>
		/// Validate a level-definition object.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public static bool ValidateLevelDefinition(JObject obj)
		{
			// must haves:
			string[] baseAttrs = { "author", "difficulty", "name", "type" };
			foreach(string str in baseAttrs)
				if (obj[str] == null)
					return false;

			// Validate type
			string type = (string)obj["type"];
			SC_Level.LevelType etype = SC_Level.LevelType.research;
			try {
				etype = (SC_Level.LevelType)Enum.Parse(typeof(SC_Level.LevelType), type);
			} catch(Exception) {
				return false;
			}

			switch (etype)
			{
			case SC_Level.LevelType.research:
				// test research elements
				string[] resAttrs = { "input-zones", "output-zones", "bonder-count", 
					"has-large-output", "has-sensor", "has-fuser", "has-splitter", "has-teleporter"
				};
				foreach (string str in resAttrs)
					if (obj[str] == null)
						return false;
					
				break;

			case SC_Level.LevelType.production:
				// test production elements
				string[] prodAttrs = { "terrain", "max-reactors", 
					"has-starter", "has-assembly", "has-disassembly", "has-advanced", 
					"has-nuclear", "has-superbonder", "has-recycler" };
				foreach (string str in prodAttrs)
					if (obj[str] == null)
						return false;
				break;

			default:
				return false;
			}

			return true;
		}
	}
}
