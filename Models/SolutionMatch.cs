using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SaveChem.Models
{
	public class SolutionMatch
	{
		[Flags]
		public enum ScoreFlags
		{
			Name = 1,
			DefHash = 2,
			ID = 4,
		}

		// -------------------------------------------------------------------------------------

		public string LevelID { get { return Level.LevelID; } }
		public string Name { get { return Level.Name; } }
		public string DefinitionHash { get { return Level.DefinitionHash; } }
		public ScoreFlags SortScore { get; protected set; }
		public string SortScoreString 
		{ 
			get
			{
				return ( (SortScore&ScoreFlags.ID)>0 ? "i" : "-") 
					+ ( (SortScore&ScoreFlags.DefHash)>0 ? "d" : "-") 
					+ ((SortScore&ScoreFlags.Name)>0 ? "n" : "-");
			}
		}

		protected SC_Solution _solution;
		public SC_Solution Solution
		{
			get { return _solution; }
			set
			{
				_solution = value;
				CalculateScore();
			}
		}


		protected SC_Level _level;
		public SC_Level Level
		{
			get { return _level; }
			set
			{
				_level = value;
				SortScore = CalculateScore();
			}
		}

		// -------------------------------------------------------------------------------------

		public SolutionMatch(SC_Solution sol, SC_Level level)
		{
			_solution = sol;
			Level = level;
		}

		public ScoreFlags CalculateScore()
		{
			ScoreFlags SortScore = 0;
			SortScore |= (Solution.LevelID == Level.LevelID) ? ScoreFlags.ID : 0;
			SortScore |= (Solution.DefinitionHash == Level.DefinitionHash) ? ScoreFlags.DefHash : 0;
			SortScore |= (Solution.Name == Level.Name) ? ScoreFlags.Name : 0;

			return SortScore;
		}
	}
}
