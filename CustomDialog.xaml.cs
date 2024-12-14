using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace WpfApp1
{
    public partial class CustomDialog : Window
    {
        public bool IsConfirmed { get; private set; } = false;

        public CustomDialog(string message)
        {
            InitializeComponent();
            MessageTextBlock.Text = message; // 알림 메시지 설정
        }

        private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            CloseWithAnimation(); // 애니메이션으로 닫기
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            CloseWithAnimation(); // 애니메이션으로 닫기
        }

        private void CloseWithAnimation()
        {
            // 페이드아웃 애니메이션 설정
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };

            fadeOut.Completed += (s, e) => this.Close(); // 애니메이션 완료 후 창 닫기
            this.BeginAnimation(Window.OpacityProperty, fadeOut);
        }
    }
}
