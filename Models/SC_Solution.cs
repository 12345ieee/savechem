using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using SaveChem.Utilities;

using SODict = System.Collections.Generic.Dictionary<string, object>;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data.SQLite;

namespace SaveChem.Models
{
	public class SC_Solution
	{
		// -------------------------------------------------------------------------------------
		#region constants

		public const string CurrentVersion = "SOLB0001";

		#endregion

		// -------------------------------------------------------------------------------------
		#region properties

		public bool IsValid { get; protected set; }

		public SODict Stats { get; protected set; }
		public string LevelID { get { return Convert.ToString(Stats["level_id"]); } }
		public string Name { get { return Convert.ToString(Stats["name"]); } }
		public string DefinitionHash { get { return Convert.ToString(Stats["def_hash"]); } }

		public int ReactorCount { get; protected set; }
		public int SymbolCount { get; protected set; }

		public DataTable Components { get; protected set; }
		public DataTable Members { get; protected set; }
		public DataTable Pipes { get; protected set; }
		public DataTable Annotations { get; protected set; }

		#endregion

		// -------------------------------------------------------------------------------------
		#region methods

		public SC_Solution()
		{
			Stats = new SODict();
			Components = null;
			Members = null;
			Pipes = null;
			Annotations = null;

			ReactorCount = 0;
			SymbolCount = 0;

			IsValid = false;
		}

		/// <summary>
		/// Load a solution for a level.
		/// </summary>
		/// <param name="user"></param>
		/// <param name="level"></param>
		/// <returns></returns>
		public bool Load(SC_User user, SC_Level level)
		{
			string sql, where;
			SQLiteDatabase userDB = user.DB;

			where = SQLiteDatabase.Escape(level.LevelID, false);

			SODict stats = new SODict();
			stats["version"] = SC_Solution.CurrentVersion;
			stats["level_id"] = level.LevelID;
			stats["name"] = level.Name;
			stats["def_hash"] = level.DefinitionHash;
			stats["date"] = null;
			Stats = stats;

			// --- Get solution data ---
			sql = String.Format("SELECT * FROM [Component] WHERE level_id = '{0}' ORDER BY rowid", where);
			Components = userDB.GetDataTable(sql);

			sql = @"SELECT {2} m.* FROM [{0}] m 
				JOIN [Component] c ON m.component_id = c.rowid 
				WHERE c.level_id = '{1}' ORDER BY m.rowid";
			Members = userDB.GetDataTable(String.Format(sql, "Member", where, ""));
			Pipes = userDB.GetDataTable(String.Format(sql, "Pipe", where, "m.rowid,"));
			Annotations = userDB.GetDataTable(String.Format(sql, "Annotation", where, ""));

			ReactorCount = Components.Select("type LIKE '%reactor%'").Length;
			SymbolCount = Members.Select("type LIKE 'instr%' AND type <> 'instr-start'").Length;

			IsValid = true;

			return true;
		}

		/// <summary>
		/// Export the current solution to a base64 string
		/// </summary>
		/// <returns></returns>
		public string Export()
		{
			dynamic data = new SODict();

			Stats["date"] = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm");

			data["stats"] = Stats;

			data["components"] = TableToMtx(Components);
			data["members"] = TableToMtx(Members);
			data["pipes"] = TableToMtx(Pipes);
			data["annotations"] = TableToMtx(Annotations);

			//# TODO : clean this up later.
			string outJson = SCTools.Compress(data, SCDataMode.OBJ, SCDataMode.JSON);
			byte[] outZip = SCTools.Compress(outJson, SCDataMode.JSON, SCDataMode.ZIP);
			string outBase = SCTools.CompressFull(data);

			App.Me.MyMainWindow.Debug = String.Format(@"
reactors/symbols : {3} / {4}
json : {0}
zip : {1}
base : {2}\n{5}", outJson.Length, outZip.Length, outBase.Length, ReactorCount, SymbolCount, outJson);

			return outBase;
		}

		/// <summary>
		/// Import solution from a base64 string
		/// </summary>
		/// <param name="solution"></param>
		/// <returns></returns>
		public bool Import(string solution)
		{
			JObject trial = (JObject)SCTools.DecompressFull(solution);
			if (trial == null)
				return false;

			// --- validation ---
			string[] baseReqs = { "stats", "components", "members", "pipes", "annotations" };
			if (!JsonValidate(trial, baseReqs))
				return false;
			string[] statsReqs = { "level_id", "name", "def_hash" };
			if (!JsonValidate(trial["stats"], statsReqs))
				return false;

			JToken obj = trial["stats"];
			string[] statProps = { "version", "level_id", "name", "def_hash", "date"};
			foreach (string s in statProps)
				Stats[s] = obj[s] ?? "--";

			// --- Import DataTables ---
			Components = MtxToTable(trial["components"]);
			Members = MtxToTable(trial["members"]);
			Pipes = MtxToTable(trial["pipes"]);
			Annotations = MtxToTable(trial["annotations"]);

			ReactorCount = Components.Select("type LIKE '%reactor%'").Length;
			SymbolCount = Members.Select("type LIKE 'instr%' AND type <> 'instr-start'").Length;

			return true;
		}

		public string Summary()
		{
			StringBuilder sb = new StringBuilder();

			foreach (var kv in Stats)
				sb.AppendFormat("{0} : {1}\n", kv.Key, kv.Value);

			sb.AppendFormat("Reactors : {0}\n", ReactorCount);
			sb.AppendFormat("Symbols : {0}\n", SymbolCount);

			return sb.ToString();
		}

		public static SODict TableToMtx(DataTable dt)
		{
			SODict dict = new SODict();
			dict["keys"] = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName);

			var x = dt.Select();
			dict["values"] = dt.Select().Select(r => r.ItemArray);

			return dict;
		}

		public static DataTable MtxToTable(SODict dict)
		{
			// --- Validate ---
			if (dict["keys"] == null || dict["values"] == null)
				return null;

			IEnumerable<string> keys = dict["keys"] as IEnumerable<string>;
			IEnumerable<object[]> values = dict["values"] as IEnumerable<object[]>;

			DataTable dt = new DataTable();
			// --- Create columns ---
			foreach (string k in keys)
			{
				DataColumn col = new DataColumn(k);
				dt.Columns.Add(col);
			}
			
			// --- Add data ---
			foreach (object[] o in values)
			{
				DataRow row = dt.NewRow();
				row.ItemArray = o;
				dt.Rows.Add(row);
			}
			return dt;
		}

		public static DataTable MtxToTable(JToken obj)
		{
			//# NOTE : there's got to be a more direct method.

			// --- Validate ---
			if (obj["keys"] == null || obj["values"] == null)
				return null;

			var keys = obj["keys"];
			var values = obj["values"];
			//List<string> keys = obj["keys"].ToObject<List<string>>();			// this works
			//List<object[]> values = obj["keys"].ToObject<List<object[]>>();	// this doesn't

			DataTable dt = new DataTable();

			// --- Create columns ---
			foreach (string k in obj["keys"])
			{
				DataColumn col = new DataColumn(k);
				dt.Columns.Add(col);
			}

			// --- Get the types from the top row ---
			if (values.Count() > 0)
			{
				Dictionary<JTokenType, string> types = new Dictionary<JTokenType, string>();
				types[JTokenType.Integer] = "System.Int64";
				types[JTokenType.String] = "System.String";
				types[JTokenType.Boolean] = "System.Boolean";

				JToken top = values[0];
				for (int i=0; i<top.Count(); i++)
					if (types.ContainsKey(top[i].Type) )
						dt.Columns[i].DataType = System.Type.GetType(types[top[i].Type]);
			}

			foreach (var o in values)
			{
				DataRow row = dt.NewRow();
				row.ItemArray = o.ToArray<object>();
				dt.Rows.Add(row);
			}

			return dt;
		}

		/// <summary>
		/// Test if all <paramref name="props"/> exist in <paramref name="obj"/>.
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="props"></param>
		/// <returns></returns>
		public static bool JsonValidate(JToken obj, string[] props)
		{
			if (obj == null)
				return false;

			foreach (string s in props)
				if (obj.Contains(s))
					return false;
					// throw new Exception(String.Format("Can't find object {}", s));

			return true;
		}

		#endregion
	}
}
