using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WpfApp1
{
    internal class TcpClientManager
    {
        private TcpClient client; // 서버와 연결하는 소켓
        private NetworkStream stream; // 데이터 송수신 스트림

        // 수신된 데이터를 전달하기 위한 이벤트
        public event Func<string, byte[], Task> OnDataReceived;

        private string serverIp;
        private int serverPort;
        private enum ACT {  };

        // 생성자 (서버 IP와 포트 설정)
        public TcpClientManager(string serverIp, int serverPort)
        {
            this.serverIp = serverIp;
            this.serverPort = serverPort;
        }

        // 초기화 메서드 (비동기로 서버 연결)
        public async Task<bool> Connect()
        {
            try
            {
                client = new TcpClient(); // TCP 클라이언트 생성
                await client.ConnectAsync(serverIp, serverPort); // 서버에 비동기로 연결
                stream = client.GetStream(); // 서버와의 데이터 송수신을 위한 스트림 가져오기
                Console.WriteLine($"서버에 연결되었습니다. {serverIp}:{serverPort}");

                // 데이터 수신 시작
                _ = ReceiveData(); // 비동기로 데이터 수신 시작 (Fire-and-forget 방식)
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버에 연결할 수 없습니다: {ex.Message}");
                return false;
            }
        }

        // 데이터를 서버로 비동기로 수신
        private async Task ReceiveData()
        {
            try
            {
                while (true) // 연결된 동안 계속 실행
                {
                    // 1. 헤더 읽기 (128바이트)
                    byte[] headerBuffer = new byte[128];
                    int headerBytesRead = 0;
                    while (headerBytesRead < headerBuffer.Length)
                    {
                        int bytesRead = await stream.ReadAsync(headerBuffer, headerBytesRead, headerBuffer.Length - headerBytesRead);
                        if (bytesRead == 0) throw new Exception("서버 연결이 종료되었습니다.");
                        headerBytesRead += bytesRead;
                    }

                    // 헤더 파싱 (actType, senderId, dataLength 추출)
                    string header = Encoding.UTF8.GetString(headerBuffer).TrimEnd('\0');
                    Debug.WriteLine($"{header} 헤더 수신");
                    string[] parts = header.Split('/');
                    int actType = int.Parse(parts[0]);    // 데이터 타입
                    string senderId = parts[1];          // 송신자 ID
                    int dataLength = int.Parse(parts[2]); // 본문 데이터 길이

                    // 2. 본문 데이터 읽기 (dataLength 크기만큼)
                    byte[] bodyBuffer = new byte[dataLength];
                    int totalBytesRead = 0;
                    while (totalBytesRead < dataLength)
                    {
                        int bytesRead = await stream.ReadAsync(bodyBuffer, totalBytesRead, dataLength - totalBytesRead);
                        if (bytesRead == 0) throw new Exception("서버 연결이 종료되었습니다.");
                        totalBytesRead += bytesRead;
                    }

                    // 3. 데이터 처리
                    if (OnDataReceived != null)
                    {
                        string body = Encoding.UTF8.GetString(bodyBuffer); // UTF-8로 디코딩
                        Debug.WriteLine($"{body} 문자열 수신");
                        await OnDataReceived($"{actType}/{senderId}/{body}", null);

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버 연결이 종료되었습니다: {ex.Message}"); // 연결 종료 로그 출력
            }
        }

        // 데이터를 서버로 전송
        public async Task SendData(int actType, string senderId = "", string msg = "")
        {
            try
            {
                // 1. 메시지 데이터 준비
                string body = string.IsNullOrEmpty(msg) ? "" : msg; // 메시지가 없으면 빈 문자열로 처리
                byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

                // 2. 헤더 생성 (128바이트: actType, senderId, 데이터 길이 포함)
                string header = $"{actType}/{senderId}/{bodyBytes.Length}";
                byte[] headerBytes = Encoding.UTF8.GetBytes(header.PadRight(128, '\0')); // 128바이트로 고정 길이 패딩

                // 3. 헤더와 본문 데이터 결합
                byte[] dataToSend = new byte[headerBytes.Length + bodyBytes.Length];
                Array.Copy(headerBytes, 0, dataToSend, 0, headerBytes.Length); // 헤더 복사
                Array.Copy(bodyBytes, 0, dataToSend, headerBytes.Length, bodyBytes.Length); // 본문 복사

                // 4. 데이터 전송
                await stream.WriteAsync(dataToSend, 0, dataToSend.Length);
                Console.WriteLine($"데이터 전송 완료: {dataToSend.Length}바이트 전송");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"데이터 전송 실패: {ex.Message}");
            }
        }

        // 연결 종료
        public void Disconnect()
        {
            try
            {
                stream?.Close(); // 스트림 닫기
                client?.Close(); // 클라이언트 소켓 닫기
                Console.WriteLine("서버와의 연결이 종료되었습니다.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"연결 종료 실패: {ex.Message}");
            }
        }
    }
}