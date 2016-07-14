using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using HangToasts.Properties;

namespace HangToasts
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string AppId = "HangToasts";

        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private readonly HangfireQueueManager _hangfireQueueManager;
        private ToastNotification _currentToast;
        private WindowState _storedWindowState = WindowState.Normal;

        public MainWindow()
        {
            InitializeComponent();

            _hangfireQueueManager = new HangfireQueueManager(Settings.Default.PollingInterval);
            _hangfireQueueManager.Change += HangfireQueueManagerOnChange;

            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.BalloonTipText = "The app has been minimised. Click the tray icon to show.";
            _notifyIcon.BalloonTipTitle = "HangToasts";
            _notifyIcon.Text = "The App";
            Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/HangToasts;component/Resources/App.ico")).Stream;
            _notifyIcon.Icon = new System.Drawing.Icon(iconStream);

            _notifyIcon.Click += new EventHandler(NotifyIcon_Click);
        }

        private void MainWindow_OnStateChanged(object sender, EventArgs args)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (_notifyIcon != null)
                    _notifyIcon.ShowBalloonTip(2000);
            }
            else
                _storedWindowState = WindowState;
        }

        private void MainWindow_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            CheckTrayIcon();
        }

        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = _storedWindowState;
        }

        private void CheckTrayIcon()
        {
            ShowTrayIcon(!IsVisible);
        }

        private void ShowTrayIcon(bool show)
        {
            if (_notifyIcon != null)
                _notifyIcon.Visible = show;
        }
        
        private void MainWindown_OnClosing(object sender, CancelEventArgs args)
        {
            _notifyIcon.Dispose();
            _notifyIcon = null;
            _hangfireQueueManager.Dispose();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _hangfireQueueManager.Initialise();
        }

        private void HangfireQueueManagerOnChange(object sender, HangfireQueuesChangedEventArgs hangfireQueuesChangedEventArgs)
        {
            // hide the current toast
            if (_currentToast != null)
                _currentToast.ExpirationTime = DateTimeOffset.Now;

            CreateToast(_hangfireQueueManager.ToastRepresentation());
        }

        private bool CreateToast(string queuedJobs)
        {
            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText01);

            // Fill in the text elements
            XmlNodeList stringElements = toastXml.GetElementsByTagName("text");
            stringElements[0].AppendChild(toastXml.CreateTextNode(queuedJobs));

            // Specify the absolute path to an image
            //string imagePath = "file:///" + Path.GetFullPath("Resources/App.png");
            //XmlNodeList imageElements = toastXml.GetElementsByTagName("image");
            //imageElements[0].Attributes.GetNamedItem("src").NodeValue = imagePath;

            _currentToast = new ToastNotification(toastXml);
            _currentToast.Dismissed += Toast_Dismissed;
            _currentToast.Activated += Toast_Activated;
            ToastNotificationManager.CreateToastNotifier(AppId).Show(_currentToast);

            return true;
        }

        private void Toast_Activated(ToastNotification sender, object args)
        {
            Debug.WriteLine("Toast activated");
        }

        private void Toast_Dismissed(ToastNotification sender, ToastDismissedEventArgs args)
        {
            Debug.WriteLine("Toast dismissed");
        }
    }
}
