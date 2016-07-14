using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.ComponentModel;
using SaveChem.Utilities;
using SaveChem.Models;
using System.Data.SQLite;
using System.Data;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;

using SSDict = System.Collections.Generic.Dictionary<string, string>;

namespace SaveChem
{
	/// <summary>
	/// Interaction logic for ImportSolutionDialog.xaml
	/// </summary>
	public partial class ImportSolutionDialog : Window, INotifyPropertyChanged
	{
		[Flags] 
		public enum SolutionState
		{
			Unknown = 0,
			Known = 1<<0,
			Core = 0<<1,
			Custom = 1<<1,
			Unplayed = 0<<2,
			Played = 1<<2,
			CoreUnplayed = Core | Unplayed,
			CorePlayed = Core | Played,
			CustomPlayed = Custom | Played,
		};

		// -------------------------------------------------------------------------------------
		#region properties

		protected bool _canApply;
		public bool CanApply
		{
			get { return _canApply; }
			protected set { _canApply = value; notify("CanApply"); }
		}

		protected bool _CanParse;
		public bool CanParse
		{
			get { return _CanParse; }
			protected set { _CanParse = value; notify("CanParse"); }
		}

		protected bool _isCustom;
		public bool IsCustom
		{
			get { return _isCustom; }
			protected set { _isCustom = value; notify("IsCustom"); }
		}

		protected bool _hasSolution;
		public bool HasSolution
		{
			get { return _hasSolution; }
			protected set { _hasSolution = value; notify("HasSolution"); }
		}

		protected bool _copyLevel;
		public bool CopyLevel
		{
			get { return _copyLevel; }
			set { _copyLevel = value; notify("CopyLevel"); }
		}

		protected string _copyName;
		public string CopyName
		{
			get { return _copyName; }
			set { _copyName = value; notify("CopyName"); }
		}


		public SC_User User { get; protected set; }
		public SC_Solution Solution { get; protected set; }

		public SolutionState State { get; protected set; }

		protected List<SolutionMatch> Matches { get; set; }

		#endregion

		// -------------------------------------------------------------------------------------

		public ImportSolutionDialog(SC_User user)
		{
			CanApply = false;
			CanParse = false;
			HasSolution = false;
			IsCustom = false;
			CopyLevel = false;
			CopyName = "";

			User = user;
			Solution = new SC_Solution();
			State = 0;
			Matches = new List<SolutionMatch>();

			DataContext = this;

			InitializeComponent();
		}

		protected bool DoParse(string solString)
		{
			SC_Solution sol = Solution;

			bool ok = sol.Import(solString);
			if (!ok)
			{
				MessageBox.Show("Can't read solution :(");
				return false;
			}

			infoText.Text = sol.Summary();
			IsCustom = SC_Level.IsCustomID(sol.LevelID);
			State = SolutionState.Unknown;

			// Prepare selections
			if (IsCustom)
			{
				State = SolutionState.Custom;

				ObservableCollection<int> x = new ObservableCollection<int>();

				List<SC_Level> matches = User.Levels.Where(o =>
					(o.LevelID.StartsWith("custom-") &&
						(o.LevelID == sol.LevelID || o.Name == sol.Name || o.DefinitionHash == sol.DefinitionHash)
					)
				).ToList();

				Matches.Clear();
				foreach (SC_Level match in matches)
					Matches.Add(new SolutionMatch(sol, match));

				//# TODO : fix it so that the dropdown automatically updates, gaddammit.
			}
			else
			{
				State = SolutionState.Core;
				SC_Level level = User.FindLevelByID(sol.LevelID);
				if (level == null)
				{
					level = User.AddLevel(sol.LevelID, "");
					if (level == null)
					{
						State |= SolutionState.Unknown;
						MessageBox.Show("Unknown core level");
						return false;
					}

					State |= SolutionState.Known;
				}

				Matches.Clear();
				Matches.Add(new SolutionMatch(sol, level));
			}

			// Sort by score.
			Matches.Sort((x, y) =>
			{
				if (x.SortScore > y.SortScore)
					return -1;
				if (x.SortScore < y.SortScore)
					return 1;
				return x.Name.CompareTo(y.Name);
			});

			// Attach and auto-selecct
			selectDropdown.ItemsSource = Matches;
			if (Matches.Count == 1)
				selectDropdown.SelectedItem = Matches[0];
			else if (Matches.Count > 1 && Matches[0].SortScore >= Matches[1].SortScore)
				selectDropdown.SelectedItem = Matches[0];

			return true;
		}

		protected bool DoImport()
		{
			SolutionMatch match = selectDropdown.SelectedItem as SolutionMatch;

			string levelID;
			if (match == null)
				return Utils.MessageBox("No level candidate selected", "Error");

			string sql;
			string[] strs;
			SQLiteDatabase userDB = User.DB;

			// --- Copy to [ResearchNet] ---
			if (CopyLevel)
			{
				// Prepare new custom level
				string name = CopyName;
				if (name == "")
					name = match.Name;

				JObject obj = (JObject)SCTools.DecompressFull(match.Level.Definition);
				if (obj == null)
					return false;

				obj["name"] = name;
				string def = SCTools.CompressFull(obj);

				SC_Level copy = User.AddLevel(SC_Level.CreateCustomID(DateTime.UtcNow), def);
				match.Level = copy;
			}

			//# TODO : add for unknown core.

			// --- And let's go ---

			SC_Level level = match.Level;
			levelID = level.LevelID;
			string where = SQLiteDatabase.Escape(levelID);

			SQLiteConnection conn = userDB.Open();
			SQLiteCommand cmd = new SQLiteCommand(conn);
			SQLiteTransaction trans = conn.BeginTransaction();

			// --- Delete old solution ---
			//# TODO : refactor this.

			strs = new string[] { "Member", "Pipe", "Annotation" };
			sql = @"DELETE FROM [{0}] WHERE component_id IN 
				(SELECT rowid FROM [Component] WHERE level_id = '{1}')";

			// remove component parts
			foreach (string s in strs)
			{
				cmd.CommandText = String.Format(sql, s, where);
				cmd.ExecuteNonQuery();
			}

			// remove level parts
			strs = new string[] { "Component", "Undo", "UndoPtr" };
			sql = "DELETE FROM [{0}] WHERE level_id = '{1}'";
			foreach (string s in strs)
			{
				cmd.CommandText = String.Format(sql, s, where);
				cmd.ExecuteNonQuery();
			}

			// --- Insert new data ---

			// Update components
			List<long> oldIDs = new List<long>();
			foreach (DataRow row in Solution.Components.Rows)
			{
				oldIDs.Add((long)row["rowid"]);
				row["level_id"] = levelID;
			}

			//# TODO : perhaps do this without a copy and remove? how about null?
			DataTable dt = Solution.Components.Copy();
			dt.Columns.Remove("rowid");

			//# PONDER : the select in useless?
			SQLiteDataAdapter sda = new SQLiteDataAdapter("SELECT * FROM [Component]", conn);
			SQLiteCommandBuilder bld = new SQLiteCommandBuilder(sda);

			int num = sda.Update(dt);

			sql = "SELECT rowid FROM [Component] WHERE level_id='{0}'";
			DataTable newIDs = userDB.GetDataTable(String.Format(sql, where));
			// Oh fuck. ABORT, ABORT!
			if (newIDs.Rows.Count != oldIDs.Count)
			{
				trans.Rollback();
				return Utils.MessageBox("Component count mismatch", "Error");
			}

			Dictionary<long, long> idMap = new Dictionary<long, long>();
			for (int i = 0; i < oldIDs.Count; i++)
				idMap[oldIDs[i]] = (long)newIDs.Rows[i]["rowid"];

			strs = new string[] { "Member", "Pipe", "Annotation" };
			DataTable[] tables = { Solution.Members, Solution.Pipes, Solution.Annotations };
			for (int i = 0; i < strs.Length; i++)
			{
				dt = tables[i];
				if (dt.Rows.Count == 0)
					continue;

				foreach (DataRow row in dt.Rows)
					row["component_id"] = idMap[(long)row["component_id"]];

				sda.SelectCommand.CommandText = String.Format("SELECT * FROM [{0}]", strs[i]);
				bld.RefreshSchema();
				sda.Update(dt);
			}

			trans.Commit();
			userDB.ExecuteNonQuery("VACUUM");
			User.DB.Close();

			level.UndoCount = 0;

			//# TODO : reload SC_Level
			//# TODO : update main window.

			return true;
		}

		// -------------------------------------------------------------------------------------
		#region events

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void notify(string property)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null)
			{
				handler(this, new PropertyChangedEventArgs(property));
			}
		}

		#endregion

		private void inputString_TextChanged(object sender, TextChangedEventArgs e)
		{
			string str = inputStringText.Text.Trim();
			CanParse = str.Length > 0;
		}

		private void btnParse_Click(object sender, RoutedEventArgs e)
		{
			string source;
			source = inputStringText.Text;

			bool res = DoParse(inputStringText.Text);
			HasSolution = res;
			CanApply = res;
		}

		private void btnCancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void btnOK_Click(object sender, RoutedEventArgs e)
		{
			MessageBoxResult res = MessageBox.Show(
@"Importing will remove all traces of the old solution: reactors, symbols, pipes, annotations and undo.

Are you sure?", "Warning", MessageBoxButton.YesNo);

			if (res == MessageBoxResult.No)
				return;

			bool ok = DoImport();
			if (ok)
			{
				MessageBox.Show("Import complete");
				Close();
			}
		}

	}
}
