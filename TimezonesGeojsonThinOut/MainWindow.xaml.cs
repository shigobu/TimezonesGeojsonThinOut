﻿using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using COMInterfaceWrapper;
using TimeZoneConverter;
using System.Collections.Generic;
using System.Drawing;
using System.Collections.Concurrent;
using System.Linq;

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

            try
            {
                await Task.Run((Action)Execute);
                MessageBox.Show("すべて完了しました。");
            }
            catch (Exception　ex)
            {
                MessageBox.Show("エラーが発生しました。" + Environment.NewLine + ex.Message);
            }
            finally
            {
                ChangeEnable(true);
                GC.Collect();
            }

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

            SetProgressBarIsIndeterminate(false);

            SetProgressBarMax(featuresElement.GetArrayLength());
            int count = 0;
            ParallelOptions options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount / 2
            };
            ConcurrentBag<Zone> zones = new ConcurrentBag<Zone>();
            //List<Zone> zones = new List<Zone>();
            Parallel.For(0, featuresElement.GetArrayLength(), options, i =>
            //for (int i = 0; i < featuresElement.GetArrayLength(); i++)
            {
                JsonElement feature = featuresElement[i];
                JsonElement geometry = feature.GetProperty("geometry");
                JsonElement coordinates = geometry.GetProperty("coordinates");

                string typeName = geometry.GetProperty("type").GetString();
                if (typeName == "Polygon")
                {
                    string tzid = feature.GetProperty("properties").GetProperty("tzid").GetString();
                    string newJsonFileName = Path.Combine(GetOutDirectoryName(), tzid.Replace('/', '-') + ".bin");
                    string zoneName = tzid;
                    //OutBinaryFile(coordinates[0], tzid, newJsonFileName);
                    Zone tempZpne = MakeZone(coordinates[0], tzid, zoneName);
                    if (tempZpne != null)
                    {
                        zones.Add(tempZpne);
                    }
                }
                else if (typeName == "MultiPolygon")
                {
                    for (int j = 0; j < coordinates.GetArrayLength(); j++)
                    {
                        JsonElement coordinates1 = coordinates[j];

                        for (int k = 0; k < coordinates1.GetArrayLength(); k++)
                        {
                            JsonElement coordinates2 = coordinates1[k];
                            string tzid = feature.GetProperty("properties").GetProperty("tzid").GetString();
                            string newJsonFileName = Path.Combine(GetOutDirectoryName(), tzid.Replace('/', '-') + j.ToString("D2") + k.ToString("D2") + ".bin");
                            //OutBinaryFile(coordinates2, tzid, newJsonFileName);
                            string zoneName = tzid + j.ToString("D2") + k.ToString("D2");
                            Zone tempZpne = MakeZone(coordinates2, tzid, zoneName);
                            if (tempZpne != null)
                            {
                                zones.Add(tempZpne);
                            }
                        }
                    }
                }
                else
                {
                    /*何もしない*/
                }
                count++;
                SetProgressBarValue(count);
            });
            //}

            System.Threading.Thread.Sleep(100);

            string outFileName = Path.Combine(GetOutDirectoryName(), "DATA.bin");
            if (File.Exists(outFileName))
            {
                File.Delete(outFileName);
            }

            //ファイル書き出し
            using (BinaryWriter binaryWriter = new BinaryWriter(new FileStream(outFileName, FileMode.Create), System.Text.Encoding.ASCII))
            {
                Zone[] sortedZone = zones.ToList().OrderBy(x => x.Name).ToArray();
                foreach (var zone in sortedZone)
                {
                    if (zone == null)
                    {
                        continue;
                    }

                    binaryWriter.Write((zone.Tzid + "\0").ToCharArray());

                    binaryWriter.Write(zone.OffsetMin);

                    //後に続く座標のバイト数を計算
                    int pointThinByteCount = zone.Vertexes.Length * 2 * sizeof(float);
                    binaryWriter.Write(pointThinByteCount);
                    foreach (var point in zone.Vertexes)
                    {
                        binaryWriter.Write(point.X);
                        binaryWriter.Write(point.Y);
                    }
                }
            }

            /*
            //一つのファイルにまとめる
            SetProgressBarIsIndeterminate(true);
            string outFileName = Path.Combine(GetOutDirectoryName(), "DATA.bin");
            if (File.Exists(outFileName))
            {
                File.Delete(outFileName);
            }
            //ファイル列挙
            string[] filenames = Directory.GetFiles(GetOutDirectoryName(), "*.bin");
            using (BinaryWriter binaryWriter = new BinaryWriter(new FileStream(outFileName, FileMode.Create), System.Text.Encoding.ASCII))
            {
                foreach (var filename in filenames)
                {
                    using (FileStream stream = new FileStream(filename, FileMode.Open))
                    {
                        int readbyte = 0;
                        while ((readbyte = stream.ReadByte()) != -1)
                        {
                            binaryWriter.Write((byte)readbyte);
                        }
                    }
                    File.Delete(filename);
                }
            }
            */
            SetProgressBarIsIndeterminate(false);
        }

        private void OutBinaryFile(JsonElement coordinates, string tzid, string outFileName)
        {
            int pointCount = coordinates.GetArrayLength();

            short offsetMin = 0;
            try
            {
                //オフセットを計算し、分で書く。short型2バイト。
                string tzidWin = TZConvert.IanaToWindows(tzid);
                TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(tzidWin);
                offsetMin = (short)timeZoneInfo.BaseUtcOffset.TotalMinutes;
            }
            catch (Exception ex)
            {
                //タイムゾーンが見つからない場合、ファイルは作成しない。
                return;
            }

            //間引くための係数
            int coefficient = 10000;
            int numThin = 1;
            if (pointCount < coefficient)
            {
                numThin = 1;
            }
            else
            {
                numThin = pointCount / coefficient;
            }

            //ファイル書き出し
            using (BinaryWriter binaryWriter = new BinaryWriter(new FileStream(outFileName, FileMode.Create), System.Text.Encoding.ASCII))
            {
                binaryWriter.Write((tzid + "\0").ToCharArray());

                binaryWriter.Write(offsetMin);

                //後に続く座標のバイト数を計算
                int pointThinByteCount = (int)Math.Ceiling((double)pointCount / numThin) * 8;
                binaryWriter.Write(pointThinByteCount);
                for (int i = 0; i < pointCount; i += numThin)
                {
                    binaryWriter.Write(coordinates[i][0].GetSingle());
                    binaryWriter.Write(coordinates[i][1].GetSingle());
                }
            }
        }

        private Zone MakeZone(JsonElement coordinates, string tzid, string outFileName)
        {
            int pointCount = coordinates.GetArrayLength();

            short offsetMin = 0;
            try
            {
                //オフセットを計算し、分で書く。short型2バイト。
                string tzidWin = TZConvert.IanaToWindows(tzid);
                TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(tzidWin);
                offsetMin = (short)timeZoneInfo.BaseUtcOffset.TotalMinutes;
            }
            catch (Exception ex)
            {
                //タイムゾーンが見つからない場合、ファイルは作成しない。
                return null;
            }

            //間引くための係数
            int coefficient = 10000;
            int numThin = 1;
            if (pointCount < coefficient)
            {
                numThin = 1;
            }
            else
            {
                numThin = pointCount / coefficient;
            }

            Zone zone = new Zone();
            zone.Name = outFileName;
            zone.Tzid = tzid;
            zone.OffsetMin = offsetMin;

            //後に続く座標のバイト数を計算 
            List<PointF> points = new List<PointF>(pointCount / numThin);
            for (int i = 0; i < pointCount; i += numThin)
            {
                points.Add(new PointF(coordinates[i][0].GetSingle(), coordinates[i][1].GetSingle()));
            }
            zone.Vertexes = points.ToArray();

            return zone;
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

        #region コントロールの情報取得関数

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

        #endregion

        private float RoundFloat(float val)
        {
            //整数部分の桁数を計算
            int num = Math.Abs((int)Math.Truncate(val));
            int digit = (num == 0) ? 0 : ((int)Math.Log10(num) + 1);

            return (float)Math.Round(val, 6 - digit);
        }
    }
}
