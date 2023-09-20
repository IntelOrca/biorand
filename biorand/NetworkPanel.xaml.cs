using System;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using IntelOrca.Biohazard.BioRand.Network;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Interaction logic for NetworkPanel.xaml
    /// </summary>
    public partial class NetworkPanel : UserControl
    {
        private BioRandClient _client;

        public NetworkPanel()
        {
            InitializeComponent();
            UpdateView();

            var settings = RandoAppSettings.Instance;
            txtPlayerName.Text = settings.PlayerName ?? "jill_sandwich";
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
                panelMenu.Visibility = Visibility.Hidden;
                panelRoom.Visibility = Visibility.Hidden;
            }
            else if (_client.RoomId == null)
            {
                panelConnect.Visibility = Visibility.Hidden;
                panelMenu.Visibility = Visibility.Visible;
                panelRoom.Visibility = Visibility.Hidden;
                lblPlayerName.Text = _client.ClientName;
            }
            else
            {
                panelConnect.Visibility = Visibility.Hidden;
                panelMenu.Visibility = Visibility.Hidden;
                panelRoom.Visibility = Visibility.Visible;
                txtCurrentRoomId.Text = _client.RoomId;
                lblPlayerList.Text = string.Join("\n", _client.RoomPlayers);
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
    }
}
