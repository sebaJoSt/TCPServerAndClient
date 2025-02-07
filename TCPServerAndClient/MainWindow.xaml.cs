using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TCPServerAndClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public TcpService TcpService { get; set; }
        private static CancellationTokenSource _cts = new();
        private readonly ObservableCollection<TcpService.ServerResponse> responseCollection;


        private bool isDarkTheme = false;

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            isDarkTheme = !isDarkTheme;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            ResourceDictionary resources = this.Resources;

            // Update each theme resource
            resources["BackgroundColor_Light"] = isDarkTheme ?
                resources["BackgroundColor_Dark"] :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));

            resources["ForegroundColor_Light"] = isDarkTheme ?
                resources["ForegroundColor_Dark"] :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000"));

            resources["AccentColor_Light"] = isDarkTheme ?
                resources["AccentColor_Dark"] :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1565C0"));

            resources["BorderColor_Light"] = isDarkTheme ?
                resources["BorderColor_Dark"] :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));

            resources["CardBackground_Light"] = isDarkTheme ?
                resources["CardBackground_Dark"] :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));

            resources["SubtleText_Light"] = isDarkTheme ?
                resources["SubtleText_Dark"] :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));

            themeIcon.Kind = isDarkTheme ? MahApps.Metro.IconPacks.PackIconBootstrapIconsKind.Sun : MahApps.Metro.IconPacks.PackIconBootstrapIconsKind.Moon;

        }

        public MainWindow()
        {
            InitializeComponent();
            TcpService = new TcpService();
            TcpService.MessageReceived += OnMessageReceivedAsync;
            sendMessageButton.IsEnabled = false;

            // Initialize the ObservableCollection
            responseCollection = [];
            responsesDataGrid.ItemsSource = responseCollection;
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {

            _cts = new CancellationTokenSource();

            if (TcpService.StartServer())
            {
                serverInfo.Text = $"Server started on port {TcpService.ServerPort}";
                sendMessageButton.IsEnabled = true;
                startServerButton.IsEnabled = false;
                await TcpService.RunServerAsync(_cts.Token);
            }

        }

        private async void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable send button while processing
                sendMessageButton.IsEnabled = false;

                // Clear previous responses
                responseCollection.Clear();

                List<TcpService.ServerResponse> responses = [];

                if (portTextBox.Text == string.Empty)
                {
                    // Send message to all Ports and get all responses
                    responses = await TcpService.ClientSendMessageAsync(messageTextBox.Text);

                }
                else
                {
                    // Send message to a specific Port and get the response
                    var response = await TcpService.ClientSendMessageToPortAsync(messageTextBox.Text, Convert.ToInt32(portTextBox.Text));
                    if (response != null)
                    {
                        responses.Add(response);
                    }
                }

                // Add new responses to the collection
                foreach (var response in responses)
                {
                    // Add to UI thread since ObservableCollection must be modified on UI thread
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        responseCollection.Add(response);
                    });
                }

                // Clear the message textbox after sending
                //messageTextBox.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                sendMessageButton.IsEnabled = true;
            }
        }

        public static Task<string> OnMessageReceivedAsync(string message)
        {
            Debug.WriteLine($"Handling received message: {message}");
            // Add your custom message handling and response logic here

            if (message.Trim() == "abc")
            {
                return Task.FromResult("def");
            }

            return Task.FromResult(message); // Echo the message back if no specific handling is needed
        }
    }
}
