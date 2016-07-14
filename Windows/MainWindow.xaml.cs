using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using SaveChem.Utilities;
using Microsoft.Win32;
using SaveChem.Models;
using System.ComponentModel;
using SaveChem.Windows;

namespace SaveChem
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		protected bool _canClearUndo;
		public bool CanClearUndo
		{ 
			get { return _canClearUndo; } 
			protected set { _canClearUndo = value; notify("CanClearUndo"); }
		}

		protected bool _canExport;
		public bool CanExport
		{
			get { return _canExport; }
			protected set { _canExport = value; notify("CanExport"); }
		}

		public bool HasSelected { get; protected set; }
		public bool HasSingleSelected { get; protected set; }
		public bool HasMultiSelected { get; protected set; }

		protected string _dbg;
		public string Debug
		{
			get { return _dbg; }
			set { _dbg = value; notify("Debug"); }
		}

		public bool HasUser { get { return App.Me.User != null; } }

		public Visibility DebugShow { get { return App.Me.DebugShow; } }

		// -------------------------------------------------------------------------------------

		public MainWindow()
		{
			CanClearUndo = false;
			CanExport = false;

			HasSelected = false;
			HasSingleSelected = false;
			HasMultiSelected = false;


			DataContext = this;

			InitializeComponent();

			Debug = PathMgr.AppRoot;
		}

		private void savegameOpen(object sender, ExecutedRoutedEventArgs e)
		{
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.DefaultExt = "user";
			dlg.Filter = "SpaceChem savegames (.user)|*.user";

			bool? result = dlg.ShowDialog();
			if (result == true)
			{
				SC_User user = new SC_User();
				if (!user.Load(dlg.FileName))
					return;

				LevelsGrid.ItemsSource = user.Levels;
				App.Me.User = user;
				notify("HasUser");

				Debug = user.Debug;

			}
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

		private void LevelsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			DataGrid grid = e.OriginalSource as DataGrid;

			int total = 0;
			int added = e.AddedItems.Count;
			int removed = e.RemovedItems.Count;
			List<SC_Level> list = grid.SelectedItems.Cast<SC_Level>().ToList();
			if (list != null)
				total += list.Count;

			CanClearUndo = total != 0;
			CanExport = total == 1;

			HasSelected = (total > 0);
			HasSingleSelected = (total == 1);
			HasMultiSelected = (total > 1);

			notify("HasSelected");
			notify("HasSingleSelected");
			notify("HasMultiSelected");

			// debug.Text = String.Format("curr {0}, add {1}, del {2}", total, added, removed);
		}

		private void btnClearUndo_Click(object sender, RoutedEventArgs e)
		{
			DataGrid grid = LevelsGrid;
			List<SC_Level> list = grid.SelectedItems.Cast<SC_Level>().ToList();

			long sum = list.Sum(o => o.UndoCount);

			MessageBoxResult res = MessageBox.Show(
				"Remove " + sum + " lines of undo information, are you sure?", 
				"Warning",
				MessageBoxButton.OKCancel);

			if (res != MessageBoxResult.OK)
				return;

			App.Me.User.ClearUndo(list);
			grid.Items.Refresh();
		}

		private void btnExport_Click(object sender, RoutedEventArgs e)
		{
			SC_Level level = LevelsGrid.SelectedItem as SC_Level;

			SC_Level dup = level.Copy();

			SC_Solution solution = new SC_Solution();
			solution.Load(App.Me.User, level);

			string dst = solution.Export();

			Clipboard.SetText(dst);
		}

		private void btnImportSolution_Click(object sender, RoutedEventArgs e)
		{
			ImportSolutionDialog dlg = new ImportSolutionDialog(App.Me.User);
			dlg.Owner = this;
			dlg.Show();
		}

		private void btnImportLevel_Click(object sender, RoutedEventArgs e)
		{
			ImportLevelDialog dlg = new ImportLevelDialog(App.Me.User);
			dlg.Owner = this;
			dlg.Show();
		}

		private void btnSlice_Click(object sender, RoutedEventArgs e)
		{
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Filter = "Spacechem savegames (*.user)|*.user|All files (*.*)|*.*";
			bool res;

			if (dlg.ShowDialog() == true)
			{
				MessageBoxResult dlgRes = MessageBox.Show(
"Create a slice of the current savegame, are you sure?", "Notice");
				if (dlgRes != MessageBoxResult.OK)
					return;

				List<string> list = LevelsGrid.SelectedItems.Cast<SC_Level>().Select(o => o.LevelID).ToList();
				res = App.Me.User.Slice(dlg.FileName, list);

			}
		}

		#endregion

		private void menuHelpAbout_Click(object sender, RoutedEventArgs e)
		{
			AboutBox box = new AboutBox();
			box.ShowDialog();
		}

	}
}
