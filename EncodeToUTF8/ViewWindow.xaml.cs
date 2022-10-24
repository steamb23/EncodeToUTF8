using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SteamB23.EncodeToUTF8
{
    /// <summary>
    /// ViewWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ViewWindow : Window
    {
        private FileData OriginalFileData { get; set; }
        private FileData FileData { get; set; }

        private static List<Encoding> Encodings { get; }

        private bool loaded = false;

        static ViewWindow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encodings = Encoding.GetEncodings()
                .Select(item => item.GetEncoding())
                .Append(Encoding.GetEncoding("euc-kr"))
                .Append(Encoding.GetEncoding("euc-jp"))
                .Append(Encoding.GetEncoding("euc-cn"))
                .OrderBy(item => item.EncodingName)
                .ToList();
        }

        public ViewWindow(FileData fileData)
        {
            OriginalFileData = fileData;
            FileData = fileData.Clone();

            InitializeComponent();
        }

        private async Task<string> LoadStringAsync()
        {
            const long maxRead = 1024 * 1024 * 1;

            await using var fileStream = File.Open(FileData.FilePath, FileMode.Open, FileAccess.Read);

            using var streamReader = new StreamReader(fileStream, FileData.Encoding, false);
            var stringBuilder = new StringBuilder();

            var totalRead = 0L;
            var charBuffer = new char[1024];
            while (true)
            {
                var read = await streamReader.ReadAsync(charBuffer, 0, charBuffer.Length);
                if (read == 0)
                    break;
                stringBuilder.Append(charBuffer, 0, read);
                totalRead += read;
                if (totalRead > maxRead)
                    break;
            }

            return stringBuilder.ToString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Equals(FileData.Encoding, OriginalFileData.Encoding) ||
                !Equals(FileData.HasBOM, OriginalFileData.HasBOM))
            {
                var messageBoxResult = MessageBox.Show("인코딩을 변경하시겠습니까?", "변경", MessageBoxButton.OKCancel);
                if (messageBoxResult == MessageBoxResult.OK)
                {
                    OriginalFileData.Encoding = FileData.Encoding;
                    OriginalFileData.HasBOM = FileData.HasBOM;
                }
            }
            Close();
        }

        private async void EncodingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FileData.Encoding = Encodings[EncodingComboBox.SelectedIndex];

            HasBomCheckbox.IsEnabled = Equals(FileData.Encoding, Encoding.UTF8);
            if (!HasBomCheckbox.IsEnabled)
                HasBomCheckbox.IsChecked = false;

            if (loaded)
            {
                var text = await LoadStringAsync();
                TextBox.Text = text;
            }
        }

        private void HasBomCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            FileData.HasBOM = HasBomCheckbox.IsChecked.GetValueOrDefault();
        }

        private async void ViewWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            var encodingTexts = Encodings
                .Select(item => $"{item.EncodingName} [{item.HeaderName}]");
            EncodingComboBox.ItemsSource = encodingTexts;
            var index = Encodings.IndexOf(FileData.Encoding);
            EncodingComboBox.SelectedIndex = index;

            HasBomCheckbox.IsEnabled = Equals(FileData.Encoding, Encoding.UTF8);
            HasBomCheckbox.IsChecked = FileData.HasBOM;

            var text = await LoadStringAsync();
            TextBox.Text = text;

            loaded = true;
        }
    }
}