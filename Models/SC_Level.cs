using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using SaveChem.Utilities;
using System.IO.Compression;
using System.Windows;
using System.Security.Cryptography;

using SSDict = System.Collections.Generic.Dictionary<string, string>;
using Newtonsoft.Json.Linq;

namespace SaveChem.Models
{
	public class SC_Level
	{
		// -------------------------------------------------------------------------------------
		#region constants

		public enum LevelType {
			research = 0,
			production = 1,
			boss = 2,
			sandbox = 3,
		}

		#endregion

		// -------------------------------------------------------------------------------------
		#region properties

		// From core/custom data
		public int CampaignID { get; set; }
		public string CampaignName { get; protected set; }
		public string ShortName { get; set; }
		public string Name { get; set; }
		public LevelType Type { get; set; }
		public string TypeName { get; set; }

		protected string _definition;
		public string Definition
		{
			get { return _definition; }
			set
			{
				_definition = value;
				_definitionHash = CalculateDefinitionHash();
			}
		}
		public string Extra { get; set; }
		public DateTime Created { get; set; }
		public int Sort { get; set; }


		// From user data
		public string LevelID { get; set; }
		public bool Passed { get; set; }
		public bool Mastered { get; set; }

		public int LastCycles { get; set; }
		public int LastReactors { get; set; }
		public int LastSymbols { get; set; }
		public string LastScore { get { return Score(LastCycles, LastReactors, LastSymbols); } }

		public int BestCycles { get; set; }
		public int BestReactors { get; set; }
		public int BestSymbols { get; set; }
		public string BestScore { get { return Score(LastCycles, LastReactors, LastSymbols); } }

		// Additional stuff
		public int UndoCount { get; set; }

		protected string _definitionHash;
		public string DefinitionHash
		{ 
			get { return _definitionHash; }
			protected set { _definitionHash = value; }
		}

		public string Summary {
			get {
				StringBuilder sb = new StringBuilder();
				SSDict strs = null;
				List<string> a = null;

				JObject obj = SCTools.DecompressFull(Definition);

				sb.AppendLine("Name: " + Name);
				sb.AppendLine("Type: " + Type);
				sb.AppendLine("Author: " + (obj["author"] ?? "--"));

				switch(Type)
				{
				case LevelType.research:
					sb.AppendLine("Bonders: " + (obj["bonder-count"] ?? "--"));

					strs = new SSDict() {
						{ "has-large-output", "large" },
						{ "has-sensor", "sensor" },
						{ "has-fuser", "fuser" },
						{ "has-splitter", "splitter" }, 
						{ "has-teleporter", "teleporter" },
					};

					a = new List<string>();
					foreach(var el in strs)
						if(obj[el.Key] != null && Convert.ToBoolean(obj[el.Key]))
							a.Add(el.Value);

					sb.AppendLine("Features: " + String.Join(", ", a));

					break;

				case LevelType.production:
					sb.AppendLine("Terrain: " + (obj["terrain"] ?? "--"));
					sb.AppendLine("Reactor limit: " + (obj["max-reactors"] ?? "--"));

					strs = new SSDict() {
						{ "has-starter", "std (4±)" },
						{ "has-assembly", "asm (4+)" },
						{ "has-disassembly", "dsm (4-)" },
						{ "has-advanced", "adv (4±,sensor)" },
						{ "has-nuclear", "adv (4±,fuser,splitter)" },
						{ "has-superbonder", "sup (8±)" },
						{ "has-recycler", "recycler" },
					};

					a = new List<string>();
					foreach(var el in strs)
						if(obj[el.Key] != null && Convert.ToBoolean(obj[el.Key]))
							a.Add(el.Value);

					sb.AppendLine("Buildings: " + String.Join(", ", a));

					break;

				default:
					break;
				}

				return sb.ToString();
			}
		}

		#endregion

		// -------------------------------------------------------------------------------------

		public SC_Level(string levelID, string def)
		{
			Clear();
			LevelID = levelID;
			if (IsCustomID(levelID))
				Created = CustomIDTime(levelID).Value;

			if (def.Length > 0)
				attachCustomData(def);
		}

		/// <summary>
		/// Create SC_Level object from user level-data and core or custom level - data. In 
		/// principle, core and custom data is mutually exclusive, but if both are given, then core data is leading.
		/// </summary>
		/// <param name="userLevel">Data from user file</param>
		/// <param name="coreLevel">Core level data (from core.dat)</param>
		/// <param name="customLevel">Level data from custom data</param>
		public SC_Level(DataRow userLevel = null, DataRow coreLevel = null, DataRow customLevel = null)
		{
			Clear();

			// --- Init with user data ---
			if (userLevel != null)
				attachUserData(userLevel);

			// --- Init with core or custom data ---
			if (coreLevel != null)
				attachCoreData(coreLevel);
			else if (customLevel != null)
				attachCustomData(customLevel);
		}

		public void attachUserData(DataRow user)
		{
			LevelID = (string)user["id"];
			Passed = (bool)user["passed"];
			Mastered = (bool)user["mastered"];

			LastCycles = (int)user["cycles"];
			LastReactors = (int)user["reactors"];
			LastSymbols = (int)user["symbols"];

			BestCycles = Convert.ToInt32(user["best_cycles"]);
			BestReactors = Convert.ToInt32(user["best_reactors"]);
			BestSymbols = Convert.ToInt32(user["best_symbols"]);

			UndoCount = Convert.ToInt32(user["undo_count"]);
		}

		public void attachCoreData(DataRow core)
		{
			if (LevelID == "")
				LevelID = (string)core["level_id"];
			CampaignID = Convert.ToInt32(core["campaign_id"]);
			ShortName = (string)core["short"];
			Name = (string)core["name"];
			Type = (LevelType)Convert.ToInt32(core["type"]);
			Definition = Convert.ToString(core["definition"]);
			DefinitionHash = CalculateDefinitionHash();
			Extra = Convert.ToString(core["extra"]);
			Created = (DateTime)core["created"];
			Sort = Convert.ToInt32(core["sort"]);
		}


		public void attachCustomData(DataRow custom)
		{
			if (LevelID == "")
				LevelID = (string)custom["level_id"];
			Created = (DateTime)custom["when_created"];
			string def = Convert.ToString(custom["definition"]);

			attachCustomData(def);
		}

		public void attachCustomData(string def)
		{
			try
			{
				byte[] defZip = SCTools.Decompress(def, SCDataMode.BASE, SCDataMode.ZIP);
				dynamic defObj = SCTools.Decompress(defZip, SCDataMode.ZIP, SCDataMode.OBJ);

				CampaignID = 4;
				ShortName = "--";
				Name = (string)defObj["name"];
				Type = (LevelType) Enum.Parse(typeof(LevelType), (string)defObj.type);
				Definition = def;
				//DefinitionHash = CalculateDefinitionHash(def);
				Extra = "";
				// Sort = Convert.ToInt32(custom["sort"]); // not here
			}
			catch(Exception e)
			{
				MessageBox.Show(e.Message);
			}
		}

		public void Clear()
		{
			CampaignID = 0;
			CampaignName = "--";
			ShortName = "--";
			Name = "--";
			Type = LevelType.research;
			Definition = "";
			Extra = "";
			Created = DateTime.UtcNow;
			Sort = int.MaxValue;

			// From user data
			 LevelID = "";
			 Passed = false;
			 Mastered = false;

			LastCycles = 0;
			LastReactors = 0;
			LastSymbols = 0;

			BestCycles = int.MaxValue;
			BestReactors = int.MaxValue;
			BestSymbols = int.MaxValue;

			UndoCount = 0;
		}

		/// <summary>
		/// Create copy of this level, but with updated values to ID, time and solution stats.
		/// </summary>
		/// <returns></returns>
		public SC_Level Copy()
		{
			SC_Level level = new SC_Level();

			level.CampaignID = this.CampaignID;
			level.ShortName = this.ShortName;
			level.Name = this.Name;
			level.Type = this.Type;
			level.Definition = this.Definition;
			level.DefinitionHash = this.DefinitionHash;
			level.Extra = this.Extra;
			level.Created = DateTime.UtcNow;

			level.LevelID = CreateCustomID(level.Created);

			return level;
		}

		protected string Score(int cycles, int reactors, int symbols)
		{
			if (cycles <= 0 || cycles == int.MaxValue || 
				symbols < 0 ||  symbols== int.MaxValue || 
				reactors < 0 || reactors == int.MaxValue)
				return "N/A";
			else
				return String.Format("{0}-{1}-{2}", cycles, reactors, symbols);
		}

		/// <summary>
		/// Create row to insert into [Level]
		/// </summary>
		/// <remarks>WARNING : the return-type will change to DataTable once I fix SQLiteDataBase's Insert.</remarks>
		/// <returns></returns>
		public SSDict CreateLevelRow()
		{
			SSDict dict = new SSDict();
			dict["id"] = LevelID;
			dict["passed"] = Passed ? "1" : "0";
			dict["mastered"] = Mastered ? "1" : "0";
			dict["cycles"] = Convert.ToString(LastCycles);
			dict["symbols"] = Convert.ToString(LastSymbols);
			dict["reactors"] = Convert.ToString(LastReactors);
			dict["best_cycles"] = Convert.ToString(BestCycles);
			dict["best_symbols"] = Convert.ToString(BestSymbols);
			dict["best_reactors"] = Convert.ToString(BestReactors);

			return dict;
		}

		/// <summary>
		/// Create row to insert into [ResearchNet]
		/// </summary>
		/// <remarks>WARNING : the return-type will change to DataTable once I fix SQLiteDataBase's Insert.</remarks>
		/// <returns></returns>
		public SSDict CreateCustomRow()
		{
			SSDict dict = new SSDict();
			dict["level_id"] = LevelID;
			dict["when_created"] = Created.ToString("yyyy-MM-dd HH:mm:ss.fffffff");
			dict["definition"] = Definition;

			return dict;
		}

		protected string CalculateDefinitionHash()
		{
			if (Definition == "")
				return "";

			return CalculateDefinitionHash(Definition);
		}

		/// <summary>
		/// Calculate the definition hash for a SpaceChem level definition. 
		/// </summary>
		/// <param name="defBytes"></param>
		/// <returns></returns>
		public static string CalculateDefinitionHash(string def)
		{
			if (def.Length == 0)
				return "";

			dynamic defObj = SCTools.DecompressFull(def);
			defObj["name"] = "";
			byte[] defBytes = SCTools.Compress(defObj, SCDataMode.OBJ, SCDataMode.ZIP);
			byte[] hashBytes = new MD5CryptoServiceProvider().ComputeHash(defBytes, 10, defBytes.Length-10);

			string hash = BitConverter.ToString(hashBytes);
			return hash.Replace("-", "");
		}

		/// <summary>
		/// Create level ID for custom missions, based on date.
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
		public static string CreateCustomID(DateTime date)
		{
			return "custom-" + date.Ticks;
		}

		public static DateTime? CustomIDTime(string levelID)
		{
			if (!IsCustomID(levelID))
				return null;

			return new DateTime(Convert.ToInt64(levelID.Substring(7)));
		}

		public static bool IsCustomID(string levelID)
		{
			return levelID.StartsWith("custom-");
		}
	}
}
