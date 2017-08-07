using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace LN2Jack
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow
    {
        private bool _isWorking;
        private volatile bool _isErrorOccurred;

        public MainWindow()
        {
            InitializeComponent();
            _isWorking = false;
            _isErrorOccurred = false;
        }

        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                return;

            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);

            PathTextBox.Text = files?[0];

            if (string.IsNullOrEmpty(DirTextBox.Text))
                DirTextBox.Text = Path.GetDirectoryName(PathTextBox.Text);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = @".osu files (*.osu)|*.osu|All files(*.*)|*.*",
                RestoreDirectory = true
            };

            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PathTextBox.Text = ofd.FileName;
                if (string.IsNullOrEmpty(DirTextBox.Text))
                    DirTextBox.Text = Path.GetDirectoryName(ofd.FileName);
            }
        }

        private void DirOpenButton_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new FolderBrowserDialog();

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                DirTextBox.Text = fbd.SelectedPath;
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (_isWorking)
                return;

            var workerThread = new Thread(Worker);

            Ln2JackWindow.Title = "Processing...";
            workerThread.Start();
        }
    }
}