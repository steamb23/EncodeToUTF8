using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
        }


        private bool includeBom = false;

        public bool IncludeBom
        {
            get => includeBom;
            set
            {
                includeBom = value;
                OnPropertyChanged();
            }
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            SetTip(null);
            DataContext = this;
        }

        private List<FileData> fileDataList = new();
        private List<FileData> viewFileDataList = new();
        private bool taskSafeStop = false;
        private bool isClosing = false;

        private Task? processTask = null;

        private readonly List<ViewWindow> viewWindows = new();

        int processCount = 0;

        public void SetTip(string? tip)
        {
            if (string.IsNullOrEmpty(tip))
            {
                tip = "파일 및 디렉토리를 끌어다 놓거나 찾아보기 단추를 이용해 파일을 추가하세요.";
            }

            Tip.Text = tip;
        }

        #region 인코딩 분석

        private byte[] bomBytes = { 0xEF, 0xBB, 0xBF };
        private byte[] bomBuffer = new byte[3];

        private void BeginProcess()
        {
            EncodingDataGrid.AllowDrop = false;
            Cursor = Cursors.Wait;
            Grid.IsEnabled = false;
        }

        private void EndProcess()
        {
            EncodingDataGrid.AllowDrop = true;
            Cursor = Cursors.Arrow;
            Grid.IsEnabled = true;
        }

        private void StartFileEncodingCheck(string[] files)
        {
            if (processTask != null)
                return;

            BeginProcess();
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
                    EndProcess();
                    StatusText.Text = $"완료";
                    SetTip(null);
                    DataGridRefresh();

                    processTask = null;
                });
            });
        }

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

        #endregion

        #region EncodingDataGrid 관련 이벤트

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

        private void EncodingDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EncodingDataGrid.SelectedItem is not FileData fileData) return;
            // Process.Start("notepad.exe", fileData.FilePath);
            var viewWindow = new ViewWindow(fileData)
            {
                Owner = this
            };
            viewWindows.Add(viewWindow);
            viewWindow.Show();
            viewWindow.Closed += (o, args) =>
            {
                if (isClosing) return;
                viewWindows.Remove(viewWindow);
                DataGridRefresh();
            };
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
            BeginProcess();
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
            EndProcess();
            StatusText.Text = $"완료";
            SetTip(null);
            DataGridRefresh();

            processTask = null;

            DataGridRefresh();
        }

        public void DataGridRefresh()
        {
            viewFileDataList.Clear();
            viewFileDataList.AddRange(fileDataList);
            foreach (var column in EncodingDataGrid.Columns)
            {
                column.SortDirection = null;
            }

            EncodingDataGrid.Items.SortDescriptions.Clear();
            EncodingDataGrid.Items.Refresh();
            TotalCountText.Text = $"대상 파일: {viewFileDataList.Count} 개";
        }

        #endregion

        #region 버튼 클릭 이벤트

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

        #endregion

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Q && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && processTask != null)
            {
                SetTip("작업을 중단중입니다.");
                StatusText.Text = $"중단 중...";
                taskSafeStop = true;
            }
        }

        private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            isClosing = true;
            foreach (var viewWindow in viewWindows)
            {
                viewWindow.Close();
            }
        }

        private void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(this,
                "목록에 바이너리 파일이 포함되어 있지 않은지 확인해주세요.\n" +
                "또한 백업되지 않은 파일이 영구적으로 손상될 수도 있습니다.",
                "영구적 파일 수정 경고!", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK)
                return;
#pragma warning disable CS4014
            Convert();
#pragma warning restore CS4014
        }

        private async Task Convert()
        {
            BeginProcess();
            SetTip("Ctrl + Q 키를 눌러서 변환을 중단할 수 있습니다.");
            await Task.Run(async () =>
            {
                var utf8Encoding = new UTF8Encoding(includeBom);
                for (var i = 0; i < fileDataList.Count; i++)
                {
                    var iCopy = i;
                    if (taskSafeStop)
                    {
                        break;
                    }

                    var fileData = fileDataList[i];
                    Dispatcher.InvokeAsync(() => StatusText.Text = $"{iCopy + 1}개 변환 중: {fileData.FilePath}");
                    if (fileData.Encoding == utf8Encoding)
                        continue;

                    try
                    {
                        var fileString = File.ReadAllTextAsync(fileData.FilePath, fileData.Encoding);
                        await File.WriteAllTextAsync(fileData.FilePath, await fileString, utf8Encoding);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }

                    var newFileData = FileEncodingCheck(fileData.FilePath);
                    if (newFileData != null)
                    {
                        fileData.Encoding = newFileData.Encoding;
                        fileData.HasBOM = newFileData.HasBOM;
                    }
                }

                Dispatcher.InvokeAsync(async () =>
                {
                    taskSafeStop = false;
                    EndProcess();
                    StatusText.Text = $"완료";
                    SetTip(null);
                    DataGridRefresh();
                });
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void ClearButton_OnClick(object sender, RoutedEventArgs e)
        {
            fileDataList.Clear();
            DataGridRefresh();
        }
    }
}