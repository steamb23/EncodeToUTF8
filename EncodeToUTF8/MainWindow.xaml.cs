using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using UtfUnknown;

namespace SteamB23.EncodeToUTF8
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private List<FileData> fileDataList = new();
        private List<FileData> viewFileDataList = new();
        private bool taskSafeStop = false;

        private Task? processTask = null;

        private readonly UTF8Encoding utf8Encoding = new UTF8Encoding(false);

        int processCount = 0;

        public void SetTip(string? tip)
        {
            if (string.IsNullOrEmpty(tip))
            {
                tip = "파일 및 디렉토리를 끌어다 놓거나 찾아보기 단추를 이용해 파일을 추가하세요.";
            }

            Tip.Text = tip;
        }

        private byte[] bomBytes = { 0xEF, 0xBB, 0xBF };
        private byte[] bomBuffer = new byte[3];

        public void FileEncodingCheck(string[] filePaths, List<FileData> fileDataList)
        {
            foreach (var filePath in filePaths)
            {
                if (taskSafeStop)
                {
                    break;
                }

                // Dispatcher.Invoke(() => { StatusText.Text = $"처리 중: {filePath}"; });
                // Debug.WriteLine($"처리 중: {filePath}");
                Dispatcher.InvokeAsync(() => StatusText.Text = $"{++processCount}개 분석 중: {filePath}");
                if (Directory.Exists(filePath))
                {
                    FileEncodingCheck(Directory.GetFiles(filePath), fileDataList);
                    FileEncodingCheck(Directory.GetDirectories(filePath), fileDataList);
                }
                else
                {
                    var fileInfo = new FileInfo(filePath);
                    // 1MB 이상 필터링
                    if (fileInfo.Length > 1024 * 1024 * 1)
                        break;
                    var fileData = FileEncodingCheck(filePath);
                    if (fileData == null)
                        continue;

                    fileDataList.Add(fileData);
                }
            }
        }

        private FileData? FileEncodingCheck(string filePath)
        {
            var detectionResult = CharsetDetector.DetectFromFile(filePath);
            if (detectionResult.Detected == null)
                return null;
            var encoding = detectionResult.Detected.Encoding;

            var hasBom = false;

            // 인코딩 업그레이드
            if (Equals(encoding, Encoding.ASCII))
            {
                encoding = Encoding.UTF8;
            }

            if (Equals(encoding, Encoding.UTF8))
            {
                // bom 체크
                using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read);
                var read = fileStream.Read(bomBuffer, 0, 3);
                if (bomBuffer.SequenceEqual(bomBytes))
                    hasBom = true;
            }

            return new FileData(filePath, encoding, hasBom);
        }

        private void StartFileEncodingCheck(string[] files)
        {
            if (processTask != null)
                return;

            EncodingDataGrid.AllowDrop = false;
            Cursor = Cursors.Wait;
            Grid.IsEnabled = false;
            SetTip("Ctrl + Q 키를 눌러서 분석을 중단할 수 있습니다.");

            processTask = Task.Run(() =>
            {
                processCount = 0;
                FileEncodingCheck(files, fileDataList);
                // 중복 제거 작업
                Dispatcher.Invoke(() => StatusText.Text = $"마무리하는 중...");
                fileDataList = fileDataList.DistinctBy(fileData => fileData.FilePath).ToList();
            });
            //timer.Start();
            processTask.GetAwaiter().OnCompleted(() =>
            {
                //timer.Stop();
                Dispatcher.Invoke(() =>
                {
                    taskSafeStop = false;
                    Grid.IsEnabled = true;
                    EncodingDataGrid.AllowDrop = true;
                    Cursor = Cursors.Arrow;
                    StatusText.Text = $"완료";
                    SetTip(null);
                    DataGridRefresh();

                    processTask = null;
                });
            });
        }

        private void EncodingDataGrid_Drop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files is not { Length: not 0 })
                return;

            StartFileEncodingCheck(files);
        }

        private void EncodingDataGrid_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void EncodingDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            EncodingDataGrid.ItemsSource = viewFileDataList;
        }

        public void DataGridRefresh()
        {
            viewFileDataList.Clear();
            viewFileDataList.AddRange(fileDataList);
            EncodingDataGrid.Items.Refresh();
            TotalCountText.Text = $"대상 파일: {viewFileDataList.Count} 개";
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EncodingDataGridSelectedItemRemove();
        }

        private async void ReAnalyseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await EncodingDataGridSelectedItemReAnalyze();
        }

        private void EncodingDataGridSelectedItemRemove()
        {
            var columnsCount = EncodingDataGrid.Columns.Count;
            var selectedCells = EncodingDataGrid.SelectedCells;
            for (var i = 0; i < selectedCells.Count / columnsCount; i++)
            {
                var selectedItem = selectedCells[i * columnsCount];
                if (selectedItem.Item is FileData fileData)
                {
                    fileDataList.Remove(fileData);
                }
            }

            DataGridRefresh();
        }

        private async Task EncodingDataGridSelectedItemReAnalyze()
        {
            EncodingDataGrid.AllowDrop = false;
            Cursor = Cursors.Wait;
            Grid.IsEnabled = false;
            SetTip("Ctrl + Q 키를 눌러서 분석을 중단할 수 있습니다.");

            processTask = Task.Run(() =>
            {
                var columnsCount = EncodingDataGrid.Columns.Count;
                var selectedCells = EncodingDataGrid.SelectedCells;
                for (var i = 0; i < selectedCells.Count / columnsCount; i++)
                {
                    if (taskSafeStop)
                    {
                        break;
                    }

                    var selectedItem = selectedCells[i * columnsCount];
                    if (selectedItem.Item is FileData fileData)
                    {
                        var iCopy = i;
                        Dispatcher.InvokeAsync(() => StatusText.Text = $"{iCopy + 1}개 재분석 중: {fileData.FilePath}");
                        var newFileData = FileEncodingCheck(fileData.FilePath);
                        if (newFileData != null)
                        {
                            fileData.Encoding = newFileData.Encoding;
                            fileData.HasBOM = newFileData.HasBOM;
                        }
                    }
                }

                Dispatcher.InvokeAsync(() => StatusText.Text = $"마무리하는 중...");
            });

            await processTask;

            taskSafeStop = false;
            Grid.IsEnabled = true;
            EncodingDataGrid.AllowDrop = true;
            Cursor = Cursors.Arrow;
            StatusText.Text = $"완료";
            SetTip(null);
            DataGridRefresh();

            processTask = null;

            DataGridRefresh();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Q && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && processTask != null)
            {
                SetTip("작업을 중단중입니다.");
                StatusText.Text = $"중단 중...";
                taskSafeStop = true;
            }
        }

        private void EncodingDataGrid_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Delete:
                    EncodingDataGridSelectedItemRemove();
                    break;
            }
        }

        private void EncodingDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EncodingDataGrid.SelectedItem is not FileData fileData) return;
            // Process.Start("notepad.exe", fileData.FilePath);
            var viewWindow = new ViewWindow(fileData);
            viewWindow.ShowDialog();
            DataGridRefresh();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            SetTip(null);
        }

        private void FolderBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();

            dialog.Multiselect = false;
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var fileNames = dialog.FileNames.ToArray();
                StartFileEncodingCheck(fileNames);
            }
        }

        private void FileBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();

            dialog.Multiselect = true;
            dialog.IsFolderPicker = false;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var fileNames = dialog.FileNames.ToArray();
                StartFileEncodingCheck(fileNames);
            }
        }

        private void AboutButton_OnClick(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow
            {
                Owner = this
            };
            aboutWindow.ShowDialog();
        }
    }
}