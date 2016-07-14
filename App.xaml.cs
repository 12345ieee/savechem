using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using SaveChem.Utilities;
using SaveChem.Models;

namespace SaveChem
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		// -------------------------------------------------------------------------------------
		public string AppRoot { get; protected set; }

		// Global properties
		public static App Me { get { return (Application.Current) as App; } }
		public MainWindow MyMainWindow { get { return Current.MainWindow as MainWindow; } }

		public SQLiteDatabase CoreDB { get; protected set; }
		public Dictionary<int, string> CoreCampaigns { get; protected set; }

		public SC_User User { get; set; }

		public Visibility DebugShow
		{ 
#if DEBUG
		get { return Visibility.Visible; }
#else
		get { return Visibility.Collapsed; }
#endif
		}

		// -------------------------------------------------------------------------------------
		public App() : base()
		{
			AppRoot = PathMgr.AppRoot;
			User = null;

			CoreDB = new SQLiteDatabase(AppRoot + "data/core.dat");
			CoreCampaigns = new Dictionary<int,string>();

			// Init campaign table
			DataTable table = CoreDB.GetDataTable("SELECT rowid, name FROM campaigns");
			foreach (DataRow row in table.Rows)
			{
				int id = Convert.ToInt32(row["rowid"]);
				CoreCampaigns.Add(id, (string)row["name"]);
			}
		}


	}
}
