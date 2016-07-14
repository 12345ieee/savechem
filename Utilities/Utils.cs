using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SaveChem.Utilities
{
	/// <summary>
	/// Misc utilities.
	/// </summary>
	public class Utils
	{
		/// <summary>
		/// Bacause using 4 lines for a messagebox + return sucks.
		/// </summary>
		/// <param name="content"></param>
		/// <param name="caption"></param>
		/// <param name="ret"></param>
		/// <returns></returns>
		public static bool MessageBox(string content, string caption="Hi", bool ret = false)
		{
			System.Windows.MessageBox.Show(content, caption);
			return ret;
		}
	}
}
