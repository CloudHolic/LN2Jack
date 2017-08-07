using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Threading;

using LN2Jack.Beatmap;
using LN2Jack.Structures;
using LN2Jack.Util;
using MessageBox = System.Windows.Forms.MessageBox;

// ReSharper disable AssignNullToNotNullAttribute
namespace LN2Jack
{
    public partial class MainWindow
    {
        private void Worker()
        {
            var path = "";
            bool? oszChecked = false;
            bool? endSnapChecked = false;
            int beatDivisior = 4;
            string[] invalidString = { "\\", "/", ":", "*", "?", "\"", "<", ">", "|" };

            _isWorking = true;

            try
            {
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    path = PathTextBox.Text;
                    oszChecked = OszCheckBox.IsChecked;
                    endSnapChecked = EndSnapCheckBox.IsChecked;

                    if (Beat16.IsChecked == true)
                        beatDivisior = 4;
                    else if (Beat24.IsChecked == true)
                        beatDivisior = 6;
                    else if (Beat32.IsChecked == true)
                        beatDivisior = 8;

                    GlobalData.OutputDir = string.IsNullOrEmpty(DirTextBox.Text) ? Path.GetDirectoryName(path) : DirTextBox.Text;
                }));

                GlobalData.Directory = Path.GetDirectoryName(path);
                GlobalData.OsuName = Path.GetFileName(path);
                GlobalData.Map = new BeatmapInfo(path);
                GlobalData.NewOsuName = GlobalData.Map.Meta.Artist + " - " + GlobalData.Map.Meta.Title + " (" +
                                        GlobalData.Map.Meta.Creator + ") [" + GlobalData.Map.Meta.Version + "_J].osu";

                foreach (var cur in invalidString)
                    GlobalData.NewOsuName = GlobalData.NewOsuName.Replace(cur, "");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, @"Error occurred!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _isErrorOccurred = _isWorking = false;
                return;
            }

            if (GlobalData.Map.Gen.Mode != 3)
            {
                MessageBox.Show(@"This program is ONLY for Mania maps!", @"LN2Jack", MessageBoxButtons.OK);
                _isErrorOccurred = _isWorking = false;
                return;
            }

            _isErrorOccurred = PatternChange(endSnapChecked ?? false, beatDivisior);

            string[] delFiles = { GlobalData.NewOsuName };

            if (_isErrorOccurred)
            {
                MessageBox.Show(@"An error occurred. Please try again.", @"LN2Jack",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                foreach (var cur in delFiles)
                    File.Delete(Path.Combine(GlobalData.Directory, cur));
            }
            else
            {
                string[] exts = { ".osu" };

                if (oszChecked == true)
                {
                    var newDir = GlobalData.Map.Meta.Artist + " - " + GlobalData.Map.Meta.Title + "_J";
                    var zipFile = GlobalData.Map.Meta.Artist + " - " + GlobalData.Map.Meta.Title + "_J.osz";
                    string newPath, zipPath;

                    foreach (var cur in invalidString)
                    {
                        newDir = newDir.Replace(cur, "");
                        zipFile = zipFile.Replace(cur, "");
                    }

                    if (GlobalData.OutputDir == GlobalData.Directory)
                    {
                        newPath = Path.Combine(Path.GetDirectoryName(GlobalData.Directory), newDir);
                        zipPath = Path.Combine(Path.GetDirectoryName(GlobalData.Directory), zipFile);
                    }
                    else
                    {
                        newPath = Path.Combine(GlobalData.OutputDir, newDir);
                        zipPath = Path.Combine(GlobalData.OutputDir, zipFile);
                    }

                    DirectoryUtil.CopyFolder(GlobalData.Directory, newPath, exts);
                    foreach (var cur in delFiles)
                        File.Move(Path.Combine(GlobalData.Directory, cur), Path.Combine(newPath, cur));

                    ZipFile.CreateFromDirectory(newPath, zipPath);
                    Directory.Delete(newPath, true);

                }
                else if (GlobalData.OutputDir != GlobalData.Directory)
                {
                    DirectoryUtil.CopyFolder(GlobalData.Directory, GlobalData.OutputDir, exts);

                    foreach (var cur in delFiles)
                        File.Move(Path.Combine(GlobalData.Directory, cur), Path.Combine(GlobalData.OutputDir, cur));
                }

                MessageBox.Show(@"Finished!", @"LN2Jack", MessageBoxButtons.OK, MessageBoxIcon.None);
            }

            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
            {
                Ln2JackWindow.Title = "LN2Jack by CloudHolic";
                PathTextBox.Text = "";
                if (GlobalData.OutputDir == GlobalData.Directory)
                    DirTextBox.Text = "";
            }));

            _isErrorOccurred = _isWorking = false;
        }

        private bool PatternChange(bool endSnapChecked, int beatDivisior)
        {
            //try
            //{
                var curPath = Path.Combine(GlobalData.Directory, GlobalData.OsuName);
                var newPath = Path.Combine(GlobalData.Directory, GlobalData.NewOsuName);
                var msPerBeatList = GlobalData.Map.Timing.Where(cur => cur.Inherited).ToList();

                var fileString = File.ReadAllLines(curPath).ToList();
                for (var i = 0; i < fileString.Count; i++)
                {
                    if (fileString[i].StartsWith("Version:"))
                    {
                        fileString[i] = "Version:" + GlobalData.Map.Meta.Version + "_J";
                        continue;
                    }
                    if (fileString[i].StartsWith("BeatmapID"))
                    {
                        fileString[i] = "BeatmapID:-1";
                        continue;
                    }
                    if (fileString[i].StartsWith("BeatmapSetID:"))
                    {
                        fileString[i] = "BeatmapSetID:-1";
                        continue;
                    }

                    //  HitObjects
                    if (fileString[i] == "[HitObjects]")
                    {
                        //TODO: Actually do works (LN -> Jack)
                        
                        for (var j = i + 1; j < fileString.Count; j++)
                        {
                            if (string.IsNullOrEmpty(fileString[j]))
                                break;

                            var cur = fileString[j].Split(',');
                            if (Convert.ToInt32(cur[3]) < 128)
                                continue;
                            
                            var startTime = Convert.ToInt32(cur[2]);
                            var endTime = Convert.ToInt32(cur[5].Split(':')[0]);
                            var addition = cur[5].Substring(endTime.ToString().Length + 1);
                        
                            fileString.RemoveAt(j);
                            for (double k = startTime; k <= endTime; )
                            {
                                //Find current BPM.
                                var curIndex = -1;
                                for (var m = 0; m < msPerBeatList.Count; m++)
                                    if (msPerBeatList[m].Offset > k)
                                    {
                                        curIndex = m - 1;
                                        break;
                                    }

                                if (curIndex < 0)
                                    curIndex = msPerBeatList.Count - 1;

                                var temp = new[] {cur[0], cur[1], Math.Round(k).ToString(new CultureInfo("en-US")), "1", cur[4], addition};
                                fileString.Insert(j++, string.Join(",", temp));
                                
                                k += msPerBeatList[curIndex].MsPerBeat / beatDivisior;

                                if (endSnapChecked)
                                    if (Math.Abs(k - endTime) <= 1)
                                        break;
                            }
                            j--;
                        }

                        // Simple insertion sorting.
                        for (var j = i + 2; j < fileString.Count; j++)
                        {
                            for (var k = j; k > 0; k--)
                            {
                                if(Convert.ToInt32(fileString[k - 1].Split(',')[2]) > Convert.ToInt32(fileString[k].Split(',')[2]))
                                    Swap(fileString, k - 1, k);
                                else
                                    break;
                            }
                        }
                    }
                }

                File.WriteAllLines(newPath, fileString);
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message, @"Error occurred!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    return true;
            //}

            return false;
        }

        private static void Swap(IList<string> str, int i, int j)
        {
            var temp = str[i];
            str[i] = str[j];
            str[j] = temp;
        }
    }
}
