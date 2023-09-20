using System;
using System.Net.Sockets;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using IntelOrca.Biohazard.BioRand.Network;
using IntelOrca.Biohazard.BioRand.Process;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Interaction logic for NetworkPanel.xaml
    /// </summary>
    public partial class NetworkPanel : UserControl
    {
        private BioRandClient _client;
        private ReProcess _process;
        private ItemBox _lastItemBoxState;
        private Timer _timer;

        public NetworkPanel()
        {
            InitializeComponent();
            UpdateView();

            var settings = RandoAppSettings.Instance;
            txtPlayerName.Text = settings.PlayerName ?? "jill_sandwich";

            _timer = new Timer();
            _timer.Interval = 100;
            _timer.Elapsed += OnTimerElapsed;
            _timer.Enabled = true;
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() => UpdateGame());
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            var settings = RandoAppSettings.Instance;
            var playerName = txtPlayerName.Text.Trim();
            settings.PlayerName = playerName;
            settings.Save();
            var host = settings.ServerAddress ?? "localhost";
            var port = BioRandServer.DefaultPort;
            if (host.Contains(":"))
            {
                var parts = host.Split(':');
                host = parts[0];
                int.TryParse(parts[1], out port);
            }

            var btn = (Button)sender;
            btn.IsEnabled = false;
            try
            {
                _client = new BioRandClient();
                _client.RoomUpdated += OnRoomUpdated;
                await _client.ConnectAsync(host, port);
                await _client.AuthenticateAsync(playerName);
                UpdateView();
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
            {
                ShowError("Connect", "Unable to connect to server, or server is unavailable.");
            }
            catch (Exception ex)
            {
                ShowError("Connect", ex);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private void OnRoomUpdated(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateView();
            });
        }

        private void UpdateView()
        {
            if (_client != null && !_client.Connected)
                _client = null;

            if (_client == null)
            {
                panelConnect.Visibility = Visibility.Visible;
                panelMenu.Visibility = Visibility.Collapsed;
                panelRoom.Visibility = Visibility.Collapsed;
            }
            else if (_client.RoomId == null)
            {
                panelConnect.Visibility = Visibility.Collapsed;
                panelMenu.Visibility = Visibility.Visible;
                panelRoom.Visibility = Visibility.Collapsed;
                lblPlayerName.Text = _client.ClientName;
            }
            else
            {
                panelConnect.Visibility = Visibility.Collapsed;
                panelMenu.Visibility = Visibility.Collapsed;
                panelRoom.Visibility = Visibility.Visible;
                txtCurrentRoomId.Text = _client.RoomId;
                lblPlayerList.Text = string.Join("\n", _client.RoomPlayers);

                UpdateGame();
            }
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (_client == null)
                return;

            _client.Dispose();
            _client = null;
            UpdateView();
        }

        private async void btnCreateRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _client.CreateRoomAsync();
            }
            catch (Exception ex)
            {
                ShowError("Create Room", ex);
            }
            UpdateView();
        }

        private async void btnJoinRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var roomId = txtRoomId.Text.Trim();
                await _client.JoinRoomAsync(roomId);
            }
            catch (Exception ex)
            {
                ShowError("Create Room", ex);
            }
            UpdateView();
        }

        private async void btnLeaveRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _client.LeaveRoomAsync();
            }
            catch (Exception ex)
            {
                ShowError("Leave Room", ex);
            }
            UpdateView();
        }

        private void ShowError(string caption, Exception ex)
        {
            ShowError(caption, ex.Message);
        }

        private void ShowError(string caption, string message)
        {
            MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void UpdateGame()
        {
            if (_process == null)
            {
                _process = FindGame();
            }
            else if (!_process.IsRunning)
            {
                _process = null;
            }

            if (_process == null)
            {
                lblGame.Text = "Not found";
            }
            else
            {
                lblGame.Text = $"Process: {_process.Name} ({_process.Id})\nItems:";

                var itemHelper = ItemHelper.GetHelper(BioVersion.Biohazard2);
                var processHelper = ProcessHelper.GetHelper(BioVersion.Biohazard2, _process);
                var itemBox = processHelper.GetItemBox();
                foreach (var item in itemBox.Items)
                {
                    if (item.Type == 0)
                        continue;

                    lblGame.Text += $"\n    {itemHelper.GetItemName(item.Type)} x{item.Amount}";
                }

                if (_lastItemBoxState != null)
                {
                    var changes = itemBox.GetChangesFrom(_lastItemBoxState);
                    if (changes.Length != 0)
                    {

                    }
                }
                _lastItemBoxState = itemBox;
            }
        }

        private static ReProcess FindGame()
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            foreach (var process in processes)
            {
                if (process.ProcessName.Contains("bio2"))
                {
                    return new ReProcess(process);
                }
            }
            return null;
        }
    }

    internal unsafe class ReProcess : IProcess
    {
        private readonly System.Diagnostics.Process _process;

        public int Id => _process.Id;
        public string Name => _process.ProcessName;
        public bool IsRunning => !_process.HasExited;

        public ReProcess(System.Diagnostics.Process process)
        {
            _process = process;
        }

        public void ReadMemory(int offset, Span<byte> buffer)
        {
            fixed (byte* ptr = buffer)
            {
                Win32.ReadProcessMemory(_process.Handle, (IntPtr)offset, (IntPtr)ptr, (IntPtr)buffer.Length, out var readBytes);
            }
        }

        public void WriteMemory(int offset, ReadOnlySpan<byte> buffer)
        {
            fixed (byte* ptr = buffer)
            {
                Win32.WriteProcessMemory(_process.Handle, (IntPtr)offset, (IntPtr)ptr, (IntPtr)buffer.Length, out var readBytes);
            }
        }
    }
}
