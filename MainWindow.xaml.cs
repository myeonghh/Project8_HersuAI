using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace WpfApp1
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 

    public class Animal
    {
        public string Name { get; set; }       // 동물 이름
        public string CaptureTime { get; set; } // 포착 시간
        public byte[] ImageBytes { get; set; }  // 이미지 데이터
    }

    public partial class MainWindow : Window
    {
        private void ShowCustomDialog(string message)
        {
            CustomDialog dialog = new CustomDialog(message);
            dialog.ShowDialog();

        }

        private TcpClientManager clientManager; // tcp통신 클래스 객체 선언

        private enum ACT { LOGIN, SIGNUP, FINDID, FINDPW, CCTVCONNECT, VIEWCCTV, CCTVCHECK, STOPCCTV, ALARM, SHOWLOG, LOGIMG, CCTVIMG, LOGOUT };
        private bool connectSuccess;
        private string myId;
        private int presentPage = 0;

        public MainWindow()
        {
            InitializeComponent();
            // 서버 연결           
            SeverConnect();
        }

        private async void SeverConnect()
        {
            string ip = "10.10.20.105"; // 서버 IP
            int port = 12345; // 서버 포트

            clientManager = new TcpClientManager(ip, port); // TcpClientManager 객체 생성
            connectSuccess = await clientManager.Connect(); // 비동기로 초기화

            if (connectSuccess)
            {
                severConnectTextBlock.Text = $"서버연결 성공  [ IP: {ip} PORT: {port} ]";
                clientManager.OnDataReceived += HandleServerData; // 서버에서 수신한 데이터 처리
            }
            else
            {
                severConnectTextBlock.Text = "서버연결 실패";
            }
        }

        private async Task HandleServerData(string data, byte[] imgData)
        {
            // 서버에서 받은 데이터 처리
            string[] parts = data.Split('/');
            ACT actType = (ACT)int.Parse(parts[0]);
            string senderId = parts[1];
            string msg = parts[2];
            switch (actType)
            {
                case ACT.LOGIN:
                    if (msg == "LoginSuccess")
                    {
                        idTextBox.Clear();
                        pwTextBox.Clear();
                        MainTabControl.SelectedIndex = 4; // "메인 메뉴" 탭으로 이동
                        presentPage = 4;
                    }
                    else if (msg == "LoginFailed")
                    {
                        this.ShowCustomDialog("아이디 또는 비밀번호가 올바르지 않습니다.");
                    }
                    break;

                case ACT.SIGNUP:
                    if (msg == "IdDup")
                    {
                        this.ShowCustomDialog("아이디가 이미 존재합니다.");
                    }
                    else if (msg == "PhoneNumDup")
                    {
                        this.ShowCustomDialog("휴대폰번호가 이미 존재합니다.");
                    }
                    else if (msg == "SignUpSuccess")
                    {
                        this.ShowCustomDialog("회원가입 되었습니다! 로그인 하세요.");
                        signupIDTextBox.Clear();
                        signupPwTextBox.Clear();
                        signupPhoneNumTextBox.Clear();
                        MainTabControl.SelectedIndex = 0; // "로그인" 탭으로 이동
                        presentPage = 0;
                    }
                    break;
                case ACT.FINDID:
                    if (msg == "findIdDup")
                    {
                        this.ShowCustomDialog("입력하신 정보와 일치하는 아이디가 없습니다.");
                    }
                    else if (msg == "findIdSuccess")
                    {
                        string foundId = parts.Length > 3 ? parts[3] : ""; // parts[3]에 찾은 아이디가 들어있음
                        this.ShowCustomDialog($"당신의 아이디는 {foundId} 입니다.");
                    }
                    break;
                case ACT.FINDPW:
                    if (msg == "findPwDup")
                    {
                        this.ShowCustomDialog("입력하신 정보와 일치하는 회원이 없습니다.");
                    }
                    else if (msg == "findPwSuccess")
                    {
                        string foundPw = senderId; // parts[3]에 찾은 아이디가 들어있음
                        this.ShowCustomDialog($"당신의 비밀번호는 {foundPw} 입니다.");
                    }
                    break;
                case ACT.CCTVIMG:
                    await ShowCCTV(imgData);
                    break;
                case ACT.SHOWLOG: // 동물 정보 리스트                    
                    await AddAnimalInfo(senderId, imgData);
                    //logScrollViewer.ScrollToEnd();
                    break;
                case ACT.ALARM: // 동물 정보 리스트
                    // AnimalInfoPanel 초기화
                    AnimalInfoPanel.Children.Clear();
                    if (presentPage == 6)
                    {
                        await clientManager.SendData((int)ACT.SHOWLOG, myId);
                    }                        
                    CustomDialog dialog = new CustomDialog($"'{senderId}'번 CCTV에서 '{ChangeAnimalNameKorean(msg)}'{(msg == "person" ? "이" : "가")} 탐지 되었습니다.");
                    dialog.Show();            
                    break;
                case ACT.CCTVCONNECT: // CCTV 연결 시
                    if (presentPage == 5)
                    {
                        await clientManager.SendData((int)ACT.VIEWCCTV, myId, "start");
                    }
                    break;
                case ACT.STOPCCTV: // CCTV 연결 종료 시
                    if (presentPage == 5)
                    {
                        BitmapImage noSignalImage = new BitmapImage(new Uri("pack://application:,,,/noSignal.png"));
                        CCTVImage.Source = noSignalImage;
                    }
                    break;
                default:
                    break;
            }
        }

        private async Task ShowCCTV(byte[] imgData)
        {
            // byte[] 데이터를 Stream으로 변환
            using (var stream = new MemoryStream(imgData))
            {
                // Stream을 BitmapImage로 변환
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // 다른 스레드에서 접근 가능하게 설정

                // UI 컨트롤에 이미지 표시
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CCTVImage.Source = bitmap;
                });
            }
        }

        private string ChangeAnimalNameKorean(string animal)
        {
            switch (animal)
            {
                case "person":
                    animal = "사람";
                    break;
                case "gorani":
                    animal = "고라니";
                    break;
                case "birds":
                    animal = "조류";
                    break;
                case "wild_boar":
                    animal = "멧돼지";
                    break;
                default:
                    break;
            }
            return animal;
        }


        private async Task AddAnimalInfo(string data, byte[] imgData)
        {
            string[] animalData = data.Split(',');
            string animalName = ChangeAnimalNameKorean(animalData[0]);
            string captureTime = animalData[1];

            // imgData를 BitmapImage로 변환
            BitmapImage thumbnailImage = null;
            if (imgData != null && imgData.Length > 0)
            {
                using (var stream = new MemoryStream(imgData))
                {
                    thumbnailImage = new BitmapImage();
                    thumbnailImage.BeginInit();
                    thumbnailImage.CacheOption = BitmapCacheOption.OnLoad;
                    thumbnailImage.StreamSource = stream;
                    thumbnailImage.EndInit();
                    thumbnailImage.Freeze(); // 다른 스레드에서 접근 가능하게 설정
                }
            }

            var animal = new Animal
            {
                Name = animalName,
                CaptureTime = captureTime,
                ImageBytes = imgData // 이미지 데이터를 직접 저장
            };

            Button animalButton = new Button
            {
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.White
            };

            StackPanel contentPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // 동물 썸네일 (이미지)
            Border thumbnail = new Border
            {
                Width = 40,
                Height = 40,
                Background = Brushes.Gray,
                Margin = new Thickness(0, 0, 10, 0),
                Child = thumbnailImage != null ? new Image { Source = thumbnailImage, Stretch = Stretch.UniformToFill } : null
            };

            StackPanel textPanel = new StackPanel { Orientation = Orientation.Vertical };
            textPanel.Children.Add(new TextBlock { Text = animal.Name, FontWeight = FontWeights.Bold, FontSize = 12 });
            textPanel.Children.Add(new TextBlock { Text = animal.CaptureTime, FontStyle = FontStyles.Italic, FontSize = 10 });

            contentPanel.Children.Add(thumbnail);
            contentPanel.Children.Add(textPanel);

            animalButton.Content = contentPanel;

            // 클릭 이벤트 설정
            animalButton.Click += (s, e) =>
            {
                // 썸네일 클릭 시 메인 이미지 변경
                ShowAnimalDetails(animal);
            };

            // AnimalInfoPanel에 버튼 추가
            await Dispatcher.InvokeAsync(() => AnimalInfoPanel.Children.Add(animalButton));
        }


        private void ShowAnimalDetails(Animal selectedAnimal)
        {
            BitmapImage mainImage = null;

            // ImageBytes 데이터를 BitmapImage로 변환
            if (selectedAnimal.ImageBytes != null && selectedAnimal.ImageBytes.Length > 0)
            {
                using (var stream = new MemoryStream(selectedAnimal.ImageBytes))
                {
                    mainImage = new BitmapImage();
                    mainImage.BeginInit();
                    mainImage.CacheOption = BitmapCacheOption.OnLoad;
                    mainImage.StreamSource = stream;
                    mainImage.EndInit();
                    mainImage.Freeze();
                }
            }

            // 메인 이미지 표시
            SelectedAnimalDisplay.Content = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{selectedAnimal.Name}  [{selectedAnimal.CaptureTime}]",
                        FontSize = 15,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, -10, 0, 10)
                    },
                    new Image
                    {
                        Source = mainImage,
                        Width = 390,
                        Height = 293,
                        Stretch = Stretch.Uniform
                    }
                }
            };
        }


        private async void loginButton_Click(object sender, RoutedEventArgs e)
        {
            myId = idTextBox.Text.Trim();
            string password = pwTextBox.Password.Trim();

            if (string.IsNullOrEmpty(myId) || string.IsNullOrEmpty(password))
            {
                this.ShowCustomDialog("아이디와 비밀번호를 모두 입력하세요");
                return;
            }

            await clientManager.SendData((int)ACT.LOGIN, myId, password);
        }

        private async void signupButton_Click(object sender, RoutedEventArgs e)
        {
            string id = signupIDTextBox.Text.Trim();
            string password = signupPwTextBox.Password.Trim();
            string phoneNum = signupPhoneNumTextBox.Text.Trim();

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(phoneNum))
            {
                this.ShowCustomDialog("정보를 모두 입력하세요");
                return;
            }

            await clientManager.SendData((int)ACT.SIGNUP, id, $"{password},{phoneNum}");
        }

        private async void find_id_Button_Click(object sender, RoutedEventArgs e)
        {
            string find_phone = find_id_phonenum.Text.Trim();

            if (string.IsNullOrEmpty(find_phone))
            {
                this.ShowCustomDialog("정보를 모두 입력하세요");
                return;
            }

            // 서버로 FINDID 요청 전송
            await clientManager.SendData((int)ACT.FINDID, null, find_phone);
        }

        private async void find_pw_Button_Click(object sender, RoutedEventArgs e)
        {
            string findpwid = find_pw_id.Text.Trim();
            string findpwphonenum = find_pw_phonenum.Text.Trim();

            if (string.IsNullOrEmpty(findpwid) || string.IsNullOrEmpty(findpwphonenum))
            {
                this.ShowCustomDialog("정보를 모두 입력하세요");
                return;
            }

            // 서버로 FINDID 요청 전송
            await clientManager.SendData((int)ACT.FINDPW, findpwid, findpwphonenum);
        }

        // CCTV 보기 버튼 클릭 시
        private async void cctvViewButton_Click(object sender, RoutedEventArgs e)
        {
            await clientManager.SendData((int)ACT.VIEWCCTV, myId, "start");
            MainTabControl.SelectedIndex = 5; // "CCTV 보기" 탭으로 이동
            presentPage = 5;
        }

        // 로그 보기 버튼 클릭 시
        private async void logViewButton_Click(object sender, RoutedEventArgs e)
        {
            // AnimalInfoPanel 초기화
            AnimalInfoPanel.Children.Clear();
            // 서버에 로그 데이터를 요청
            await clientManager.SendData((int)ACT.SHOWLOG, myId);
            MainTabControl.SelectedIndex = 6; // "로그보기" 탭으로 이동
            presentPage = 6;
        }


        // 아이디 찾기 버튼 클릭 시
        private void OnFindIdButtonClick(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedIndex = 2; // "아이디 찾기" 탭으로 이동
            presentPage = 2;
        }

        // 비밀번호 찾기 버튼 클릭 시
        private void OnFindPasswordButtonClick(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedIndex = 3; // "비밀번호 찾기" 탭으로 이동
            presentPage = 3;
        }

        // 회원가입 버튼 클릭 시
        private void OnRegisterButtonClick(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedIndex = 1; // "회원가입" 탭으로 이동
            presentPage = 1;
        }

        // 회원가입 페이지에서 로그인 페이지 이동 버튼 클릭 시
        private void toHomeButton1_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedIndex = 0; // "로그인" 탭으로 이동 
            presentPage = 0;
        }

        // 아이디 찾기 페이지에서 로그인 페이지 이동 버튼 클릭 시
        private void toHomeButton2_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedIndex = 0; // "로그인" 탭으로 이동 
            presentPage = 0;
        }

        // 비밀번호 찾기 페이지에서 로그인 페이지 이동 버튼 클릭 시
        private void toHomeButton3_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedIndex = 0; // "로그인" 탭으로 이동 
            presentPage = 0;
        }

        // CCTV 페이지에서 메인메뉴 페이지 이동 버튼 클릭 시
        private async void toHomeButton4_Click(object sender, RoutedEventArgs e)
        {
            await clientManager.SendData((int)ACT.STOPCCTV, myId, "stop");
            MainTabControl.SelectedIndex = 4; // "로그인" 탭으로 이동 
            presentPage = 4;
        }

        // 로그 보기 페이지에서 메인메뉴 페이지 이동 버튼 클릭 시
        private void toHomeButton5_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedIndex = 4; // "로그인" 탭으로 이동 
            presentPage = 4;
        }

        // 회원 가입 아이디 초기화 버튼 클릭시
        private void signUpIdClearButton_Click(object sender, RoutedEventArgs e)
        {
            signupIDTextBox.Clear();
        }

        // 회원 가입 비밀번호 초기화 버튼 클릭시
        private void signUpPwClearButton_Click(object sender, RoutedEventArgs e)
        {
            signupPwTextBox.Clear();
        }

        // 회원 가입 휴대폰 번호 초기화 버튼 클릭시
        private void signUpPnumClearButton_Click(object sender, RoutedEventArgs e)
        {
            signupPhoneNumTextBox.Clear();
        }

        // 로그아웃 버튼 클릭 시 로그인 탭으로 이동
        private async void logoutButton_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedIndex = 0; // "로그인" 탭으로 이동
            presentPage = 0;
            await clientManager.SendData((int)ACT.LOGOUT, myId, "logout");
        }

        // 아이디 찾기 핸드폰 입력창 초기화 버튼
        private void idFindClearBtn_Click(object sender, RoutedEventArgs e)
        {
            find_id_phonenum.Clear();
        }

        // 비밀번호 찾기 아이디 입력창 초기화 버튼
        private void pwFindIdClearBtn_Click(object sender, RoutedEventArgs e)
        {
            find_pw_id.Clear();
        }

        // 비밀번호 찾기 핸드폰 번호 입력창 초기화 버튼
        private void pwFindPnumClearBtn_Click(object sender, RoutedEventArgs e)
        {
            find_pw_phonenum.Clear();
        }
    }
}
