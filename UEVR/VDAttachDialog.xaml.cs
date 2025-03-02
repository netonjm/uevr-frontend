using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace UEVR
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class VDAttachDialog : Window
    {
        public VDAttachDialog()
        {
            InitializeComponent();
        }

        internal void ShowError()
        {
           
        }

        internal void SetTitle(string text)
        {
            TitleView.Text = text;
            AllowUIToUpdate();
        }

        void AllowUIToUpdate()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(delegate (object parameter)
            {
                frame.Continue = false;
                return null;
            }), null);

            Dispatcher.PushFrame(frame);
            //EDIT:
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                          new Action(delegate { }));
        }

        internal void SetSubtitle(string text)
        {
            SubtitleView.Text = text;
            AllowUIToUpdate();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }
    }
}
