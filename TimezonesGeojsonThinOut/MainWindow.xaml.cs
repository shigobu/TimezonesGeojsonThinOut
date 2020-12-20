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
using COMInterfaceWrapper;

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

		/// <summary>
		/// 出力選択ボタン選択時のイベント。
		/// </summary>
		private void OutSelectButton_Click(object sender, RoutedEventArgs e)
		{
			FolderSelectDialog folderSelectDialog = new FolderSelectDialog();
			if (folderSelectDialog.ShowDialog())
			{
				outDirNameTextBox.Text = folderSelectDialog.Path;
			}
		}

		private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
		{
			ChangeEnable(false);

			await Task.Run((Action)Execute);

			ChangeEnable(true);
		}

		/// <summary>
		/// json間引き処理本体
		/// </summary>
		private void Execute()
		{
			string jsonFileName = GetFileName();
			if (!File.Exists(jsonFileName))
			{
				return;
			}

			string outDir = GetOutDirectoryName();
			if (!Directory.Exists(outDir))
			{
				return;
			}

			SetProgressBarIsIndeterminate(true);

			string jsonData = "";
			using (StreamReader fileStream = new StreamReader(jsonFileName))
			{
				jsonData = fileStream.ReadToEnd();
			}
			JsonDocument document = JsonDocument.Parse(jsonData);

			JsonElement featuresElement = document.RootElement.GetProperty("features");
			int maxLength = 0;
			int minLength = int.MaxValue;
			bool once = false;
			List<int> countList = new List<int>(500);

			SetProgressBarIsIndeterminate(false);

			SetProgressBarMax(featuresElement.GetArrayLength());
			for(int j = 0; j < featuresElement.GetArrayLength(); j++)
			{
				JsonElement feature = featuresElement[j];
				JsonElement geometry = feature.GetProperty("geometry");
				JsonElement coordinates = geometry.GetProperty("coordinates");

				string typeName = geometry.GetProperty("type").GetString();
				if (typeName == "Polygon")
				{
					int pointCount = coordinates[0].GetArrayLength();

					//間引くための係数
					int numThin;
					if (pointCount < 10)
					{
						numThin = 1;
					}
					else
					{
						numThin = pointCount / 10;
					}

					int pointCountThin = pointCount / numThin;

					Feature newFeature = new Feature
					{
						Tzid = feature.GetProperty("properties").GetProperty("tzid").GetString(),
						Coordinates = new float[pointCountThin/* + 1*/][]
					};

					for (int i = 0; i < pointCount; i += numThin)
					{
						//配列外の参照があるので、ガード。
						if ((i / numThin) >= newFeature.Coordinates.Length)
						{
							break;
						}
						newFeature.Coordinates[i / numThin] = new float[2];
						newFeature.Coordinates[i / numThin][0] = RoundFloat(coordinates[0][i][0].GetSingle());
						newFeature.Coordinates[i / numThin][1] = RoundFloat(coordinates[0][i][1].GetSingle());
					}

					//ファイル書き出し
					string jsonString = JsonSerializer.Serialize(newFeature);
					string newJsonFileName = Path.Combine(GetOutDirectoryName(), newFeature.Tzid.Replace('/', '-') + Path.GetExtension(jsonFileName));
					using (StreamWriter streamWriter = new StreamWriter(newJsonFileName, false))
					{
						streamWriter.Write(jsonString);
					}
					//break;
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
				SetProgressBarValue(j);
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
			outDirNameTextBox.IsEnabled = enable;
			outSelectButton.IsEnabled = enable;
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
				progress.Maximum = max;
			}
			else
			{
				progress.Dispatcher.Invoke(new SetProgressBarMaxDelegate(SetProgressBarMax), max);
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
				progress.Value = val;
			}
			else
			{
				progress.Dispatcher.Invoke(new SetProgressBarValueDelegate(SetProgressBarValue), val);
			}
		}

		delegate void SetProgressBarIsIndeterminateDelegate(bool isIndeterminate);
		/// <summary>
		/// プログレスバーの値無し状態の設定をします。
		/// </summary>
		/// <param name="isIndeterminate"></param>
		private void SetProgressBarIsIndeterminate(bool isIndeterminate)
		{
			if (progress.Dispatcher.CheckAccess())
			{
				progress.IsIndeterminate = isIndeterminate;
			}
			else
			{
				progress.Dispatcher.Invoke(new SetProgressBarIsIndeterminateDelegate(SetProgressBarIsIndeterminate), isIndeterminate);
			}
		}

		/// <summary>
		/// ファイル名テキストボックスの内容を取得します。
		/// </summary>
		/// <returns>ファイル名</returns>
		private string GetFileName()
		{
			if (fileNameTextBox.Dispatcher.CheckAccess())
			{
				return fileNameTextBox.Text;
			}
			else
			{
				return fileNameTextBox.Dispatcher.Invoke(GetFileName);
			}
		}

		/// <summary>
		/// 出力先名を取得します。
		/// </summary>
		/// <returns>フォルダ名</returns>
		private string GetOutDirectoryName()
		{
			if (outDirNameTextBox.Dispatcher.CheckAccess())
			{
				return outDirNameTextBox.Text;
			}
			else
			{
				return outDirNameTextBox.Dispatcher.Invoke(GetOutDirectoryName);
			}
		}

		private float RoundFloat(float val)
		{
			//整数部分の桁数を計算
			int num = Math.Abs((int)Math.Truncate(val));
			int digit = (num == 0) ? 0 : ((int)Math.Log10(num) + 1);

			return (float)Math.Round(val, 6 - digit);
		}
	}
}
