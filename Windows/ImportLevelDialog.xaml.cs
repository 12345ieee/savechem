using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SaveChem.Utilities;
using SaveChem.Models;
using Newtonsoft.Json.Linq;

namespace SaveChem.Windows
{
	/// <summary>
	/// Interaction logic for ImportLevelDialog.xaml
	/// </summary>
	public partial class ImportLevelDialog : ObservableWindow
	{
		protected bool _canTry;
		public bool CanTry
		{
			get { return _canTry; }
			protected set { _canTry = value; notify("CanTry"); }
		}

		protected bool _canApply;
		public bool CanApply
		{
			get { return _canApply; }
			protected set { _canApply = value; notify("CanApply"); }
		}

		protected string _summary;
		public string Summary
		{
			get { return _summary; }
			protected set { _summary = value; notify("Summary"); }
		}


		public SC_User User { get; protected set; }
		public SC_Level Level { get; protected set; }

		// -------------------------------------------------------------------------------------

		public ImportLevelDialog(SC_User user)
		{
			CanTry = false;
			CanApply = false;
			Summary = "";

			User = user;
			Level = new SC_Level();

			DataContext = this;
			InitializeComponent();
		}

		public bool DoParse(string defBase)
		{
			JObject obj = SCTools.DecompressFull(defBase);
			if (obj == null || !SCTools.ValidateLevelDefinition(obj))
			{
				Summary =
@"Input is not recognized as level.

(This may be a false negative. If it should be a valid level, send me the definition, and I'll take a closer look)
";
				return false;
			}

			Level = new SC_Level(SC_Level.CreateCustomID(DateTime.UtcNow), defBase);
			Summary = Level.Summary;

			return true;
		}

		public bool DoImport()
		{
			if (Level == null)
				return false;

			SC_Level level = User.AddLevel(Level.LevelID, Level.Definition);

			return level != null;
		}


		// -------------------------------------------------------------------------------------

		private void inputString_TextChanged(object sender, TextChangedEventArgs e)
		{
			string str = inputStringText.Text.Trim();
			CanTry = str.Length > 0;
		}

		private void btnParse_Click(object sender, RoutedEventArgs e)
		{
			string source;
			source = inputStringText.Text;

			bool res = DoParse(inputStringText.Text);

			CanApply = res;
		}

		private void btnCancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void btnOK_Click(object sender, RoutedEventArgs e)
		{
			MessageBoxResult res = MessageBox.Show(
@"Import this level?", "Warning", MessageBoxButton.YesNo);

			if (res == MessageBoxResult.No)
				return;

			bool ok = DoImport();
			if (ok)
			{
				MessageBox.Show("Level Import complete");
				Close();
			}
		}
	}
}
