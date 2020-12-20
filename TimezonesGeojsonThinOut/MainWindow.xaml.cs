using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TimezonesGeojsonThinOut
{
	/// <summary>
	/// MainWindow.xaml の相互作用ロジック
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// 選択ボタン押下時のイベント。ファイル選択と選択されたファイルの表示。
		/// </summary>
		private void SelectButton_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "json File(*.json)|*.json";
			bool? result = openFileDialog.ShowDialog();
			if ((bool)result)
			{
				fileNameTextBox.Text = openFileDialog.FileName;
			}
		}

		private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
		{
			ChangeEnable(false);

			await Task.Run((Action)Execute);

			ChangeEnable(true);
		}

		private void Execute()
		{
			string jsonFileName = Console.ReadLine();

			string jsonData = "";
			using (StreamReader fileStream = new StreamReader(jsonFileName))
			{
				jsonData = fileStream.ReadToEnd();
			}
			JsonDocument document = JsonDocument.Parse(jsonData);

			Console.WriteLine("読み込み完了");

			JsonElement featuresElement = document.RootElement.GetProperty("features");
			Console.WriteLine(featuresElement.GetArrayLength());
			int maxLength = 0;
			int minLength = int.MaxValue;
			bool once = false;
			List<int> countList = new List<int>(500);
			foreach (var item in featuresElement.EnumerateArray())
			{
				JsonElement geometry = item.GetProperty("geometry");
				JsonElement coordinates = geometry.GetProperty("coordinates");

				string typeName = geometry.GetProperty("type").GetString();
				if (typeName == "Polygon")
				{
					int temp = coordinates.GetArrayLength();
					countList.Add(temp);
					if (temp > maxLength)
					{
						maxLength = temp;
					}
					if (temp < minLength)
					{
						minLength = temp;
					}
				}
				else if (typeName == "MultiPolygon")
				{
					foreach (var coordinates1 in coordinates.EnumerateArray())
					{
						foreach (var coordinates2 in coordinates1.EnumerateArray())
						{
							if (!once)
							{
								once = true;
								Console.WriteLine(coordinates2.EnumerateArray().First());
							}

							int temp = coordinates2.GetArrayLength();
							countList.Add(temp);
							if (temp > maxLength)
							{
								maxLength = temp;
							}
							if (temp < minLength)
							{
								minLength = temp;
							}
						}
					}
				}
				else
				{
					/*何もしない*/
				}

			}
		}

		/// <summary>
		/// コントロールの有効・無効を切り替えます。
		/// </summary>
		/// <param name="enable">有効・無効</param>
		private void ChangeEnable(bool enable)
		{
			selectButton.IsEnabled = enable;
			fileNameTextBox.IsEnabled = enable;
			executeButton.IsEnabled = enable;
		}

		delegate void SetProgressBarMaxDelegate(int max);
		/// <summary>
		/// プログレスバーの最大値を設定します。
		/// </summary>
		/// <param name="max">最大値</param>
		private void SetProgressBarMax(int max)
		{
			if (progress.Dispatcher.CheckAccess())
			{
				progress.Dispatcher.Invoke(new SetProgressBarMaxDelegate(SetProgressBarMax), max);
			}
			else
			{
				progress.Maximum = max;
			}
		}

		delegate void SetProgressBarValueDelegate(int val);
		/// <summary>
		/// プログレスバーの値を設定します。
		/// </summary>
		/// <param name="val">値</param>
		private void SetProgressBarValue(int val)
		{
			if (progress.Dispatcher.CheckAccess())
			{
				progress.Dispatcher.Invoke(new SetProgressBarValueDelegate(SetProgressBarMax), val);
			}
			else
			{
				progress.Value = val;
			}
		}
	}
}
