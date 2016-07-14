using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SaveChem.Utilities
{
	public class PathMgr
	{
		/// <summary>
		/// Get the root path of the application.
		/// </summary>
		public static string AppRoot
		{
			get
			{
				string path = AppDomain.CurrentDomain.BaseDirectory;
				string dir = Path.GetDirectoryName(path);
#if DEBUG
				path = Path.GetDirectoryName(Path.GetDirectoryName(dir)) + Path.DirectorySeparatorChar;
#else
				//path= Path.GetDirectoryName(dir) + Path.DirectorySeparatorChar;
#endif

				return path;
			}
		}

	}
}
