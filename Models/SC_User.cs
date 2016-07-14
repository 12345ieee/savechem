using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SaveChem.Utilities;
using System.Windows;
using System.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.IO;
using System.Data.SQLite;
using System.Collections.ObjectModel;

using SSDict = System.Collections.Generic.Dictionary<string, string>;

namespace SaveChem.Models
{
	public class SC_User : Observable
	{
		// ------------------------------------------------------------------------------------
		#region properties

		protected static readonly string[] TABLES = { 
				"Pipe", "Annotation", "Local", "UndoPtr", "Undo", 
				"Member", "Component", "ResearchNet", "Level",  
		};

		public string Path { get; set; }

		public SQLiteDatabase DB { get; protected set; }

		public DataTable LevelTable { get; protected set; }
		public ObservableCollection<SC_Level> Levels { get; set; }

		// public List<SC_Level> CustomLevels { get; protected set; }

		protected string _debug;
		public string Debug { 
			get { return _debug; }
			set { _debug = value; notify("Debug"); }
		}

		protected List<string> _errors;
		public List<string> Errors {  get { return _errors; } }

		#endregion

		// ------------------------------------------------------------------------------------
		#region methods

		public SC_User()
		{
			DB = null;
			_errors = new List<string>();
			Levels = new ObservableCollection<SC_Level>();
		}

		public bool Load(string fpath)
		{
			SQLiteDatabase userDB = null;
			string sql;

			_errors.Clear();
			try
			{
				// --- open file ---
				// --- validate as spacechem user file ---
				userDB = new SQLiteDatabase(fpath);

				bool res = ValidateSavegame(userDB);
				if (!res)
					throw new Exception("Invalid SpaceChem savegame :(");

				List<SC_Level> levels = new List<SC_Level>();

				// --- Build list of levels ---
				//*
				// just levels:
				sql = "SELECT *, 0 AS undo_count FROM Level ORDER BY id";
				/*/
				// with undo count
				sql = @"SELECT v.*, COUNT(u.level_id) AS undo_count FROM Level v 
					LEFT JOIN Undo u ON v.id=u.level_id 
					GROUP BY v.id ORDER BY v.id";
				//*/

				DataTable userLevels = userDB.GetDataTable(sql);

				sql = @"SELECT level_id, COUNT(level_id) AS undo_count FROM Undo 
					GROUP BY level_id ORDER BY level_id";
				DataTable undos = userDB.GetDataTable(sql);

				int i = 0;
				foreach (DataRow r in undos.Rows)
				{
					string id = (string)r["level_id"];
					for (; i<userLevels.Rows.Count; i++)
					{
						if((string)userLevels.Rows[i]["id"] == id)
						{
							userLevels.Rows[i]["undo_count"] = Convert.ToInt32(r["undo_count"]);
							break;
						}
					}
				}

				// --- Combine user + core ---
				SQLiteDatabase coreDB = App.Me.CoreDB;
				DataTable coreLevels = coreDB.GetDataTable("SELECT * FROM [levels] lvl");

				var result = 
					(from u in userLevels.AsEnumerable() 
					join _c in coreLevels.AsEnumerable() 
					on u.Field<string>("id") equals _c.Field<string>("level_id") into tmp
					from c in tmp
					select new { u, c }).OrderBy(x => x.c.Field<long>("sort"));

				foreach (var row in result)
				{
					SC_Level level = new SC_Level(row.u, row.c, null);
					levels.Add(level);
				}

				// --- Combine user + custom ---
				DataTable customLevels = userDB.GetDataTable("SELECT * FROM [ResearchNet]");

				result =
					(from u in userLevels.AsEnumerable()
					join _c in customLevels.AsEnumerable()
					on u.Field<string>("id") equals _c.Field<string>("level_id") into tmp
					from c in tmp
					select new { u, c }).OrderBy(x => x.u.Field<string>("id"));

				int sort = coreLevels.Rows.Count;

				foreach (var row in result)
				{
					SC_Level level = new SC_Level(row.u, null, row.c);
					level.Sort = sort;
					levels.Add(level);
					sort++;
				}

				// create list of tables & fieldnames
				Debug = "";
				foreach (string tbl in TABLES)
				{
					Debug += tbl + "\n";
				}

				// --- create temp copy ---
				Levels = new ObservableCollection<SC_Level>(levels);
				LevelTable = userLevels;
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message);
				return false;
			}

			DB = userDB;
			Path = fpath;

			return true;
		}

		public bool Slice(string dstName, List<string> list)
		{

			try {
				// Sorry, can't slice to self (yet)
				if (Path == dstName)
					throw new Exception("Can't slice to self (yet)");

				File.Copy(this.Path, dstName);
				SC_User dst = new SC_User();
				dst.Load(dstName);
				dst.RemoveSolutions(list);
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.Message);
				return false;
			}

			return true;
		}

		public bool ValidateSavegame(SQLiteDatabase db)
		{
			// Test for the existence of certain tables
			string sql= "SELECT tbl_name FROM sqlite_master WHERE type='table'";
			DataTable dt= db.GetDataTable(sql);

			List<string> errors = new List<string>();

			string[] reqTables = { 
				"Pipe", "Annotation", "Local", "UndoPtr", "Undo", 
				"Member", "Component", "ResearchNet", "Level",  
			};

			List<string> tables = new List<string>();
			foreach (DataRow row in dt.Rows)
				tables.Add(row["tbl_name"] as string);

			foreach (string name in TABLES)
				if (!tables.Contains(name))
					errors.Add(String.Format("ERROR: cannot find table '{0}'\n", name));

			_errors.AddRange(errors);
			return errors.Count == 0;
		}

		public SC_Level FindLevelByID(string levelID)
		{
			return Levels.SingleOrDefault(o => o.LevelID == levelID);
		}

		/// <summary>
		/// Add a new level to the Level table, and to the custom list, if <paramref name="def"/> is not empty.
		/// Uses transaction.
		/// </summary>
		/// <param name="levelID"></param>
		/// <param name="def"></param>
		/// <returns></returns>
		public SC_Level AddLevel(string levelID, string def = "")
		{
			SQLiteConnection conn = DB.Open();
			SQLiteTransaction trans = conn.BeginTransaction();

			SC_Level level;
			bool isNewLevel;
			string sql;
			string where = SQLiteDatabase.Escape(levelID);

			try
			{
				// --- First, see if we have it already ---
				level = FindLevelByID(levelID);
				isNewLevel = (level == null);
				if (isNewLevel)
					level = new SC_Level(levelID, def);

				DataTable user, core, custom;

				if (SC_Level.IsCustomID(levelID))
				{
					sql = "SELECT * FROM [ResearchNet] WHERE level_id = '{0}' LIMIT 1";
					sql = String.Format(sql, where);
					custom = DB.GetDataTable(sql);
					if (custom.Rows.Count == 0)
						if (!DB.Insert("ResearchNet", level.CreateCustomRow()))
							throw new Exception("Can't add level to [ResearchNet]");
				}
				else if (isNewLevel)
				{
					sql = "SELECT * FROM [levels] WHERE level_id = '{0}' LIMIT 1";
					sql = String.Format(sql, where);
					core = App.Me.CoreDB.GetDataTable(sql);
					if (core.Rows.Count == 0)
						throw new Exception(String.Format("Unknown core level '{0}' ?!?", levelID));

					level.attachCoreData(core.Rows[0]);
				}

				// --- Add to [Level] ---
				sql = "SELECT * FROM [Level] WHERE id = '{0}' LIMIT 1";
				sql = String.Format(sql, where);
				user = DB.GetDataTable(sql);
				if (user.Rows.Count == 0)
					if (!DB.Insert("Level", level.CreateLevelRow()))
						throw new Exception("Can't add level to [Level]");


				// --- Update Levels when rest is done ---
				if (isNewLevel)
				{
					if (SC_Level.IsCustomID(levelID))
						level.Sort = Levels.Max(o => o.Sort) + 1;
					Levels.Add(level);
				}
			}
			catch (Exception)
			{
				trans.Rollback();
				return null;
			}

			trans.Commit();

			DB.Close();

			return level;
		}

		/// <summary>
		/// Remove solutions from user file.
		/// </summary>
		/// <param name="levelIDs"></param>
		/// <param name="clearStats"></param>
		/// <returns></returns>
		public bool RemoveSolutions(List<string> levelIDs)
		{
			SQLiteDatabase userDB = DB;
			SQLiteConnection conn = userDB.Open();
			SQLiteCommand cmd = new SQLiteCommand(conn);
			SQLiteTransaction trans = conn.BeginTransaction();

			string[] strs;
			string sql;
			string where = String.Join(",", levelIDs.Select(
				o => SQLiteDatabase.Escape(o, true)
			).ToArray());

			try {
				// --- Remove from DB ---
				int qres;

				// remove component parts
				strs = new string[] { "Member", "Pipe", "Annotation" };
				sql = @"DELETE FROM [{0}] WHERE component_id IN 
					(SELECT rowid FROM [Component] WHERE level_id NOT IN ({1}))";

				foreach (string s in strs)
					qres = userDB.ExecuteNonQuery(String.Format(sql, s, where));

				// remove level parts
				strs = new string[] { "Component", "Undo", "UndoPtr" };
				sql = "DELETE FROM [{0}] WHERE level_id NOT IN ({1})";

				foreach (string s in strs)
					qres = userDB.ExecuteNonQuery(String.Format(sql, s, where));

				// --- Remove from Levels ---
				// TODO (?)
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.Message);
				trans.Rollback();
				userDB.Close();

				return false;
			}

			trans.Commit();

			userDB.ExecuteNonQuery("VACUUM");
			userDB.Close();

			return true;
		}


		public bool ClearUndo(List<SC_Level> levels)
		{
			string sql;
			int result;

			try {
				// Undo all special case
				if (levels.Count == Levels.Count)	// all levels
				{
					sql = "DELETE FROM [UndoPtr]; DELETE FROM [Undo]; VACUUM;";
					result = DB.ExecuteNonQuery(sql);
				}
				else								// selected levels
				{
					levels = levels.Where(o => o.UndoCount > 0).ToList();

					string str = string.Join(",", levels.Select(
						o => SQLiteDatabase.Escape(o.LevelID, true)
					).ToArray());

					sql = String.Format(@"
					DELETE FROM [UndoPtr] WHERE level_id IN({0}); 
					DELETE FROM [Undo] WHERE level_id IN({0}); 
					VACUUM;", str);
					result = DB.ExecuteNonQuery(sql);
				}
			}
			catch(Exception e)
			{
				MessageBox.Show("Error deleting Undo information:\n\n" + e.Message);
				return false;
			}

			levels.ForEach(o => o.UndoCount = 0);
			return true;
		}

		#endregion

	}
}
