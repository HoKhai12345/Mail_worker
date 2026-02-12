using MailKit.Net.Smtp;
using MimeKit;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace MailWorkerService
{
    public class UserMailTask
    {
        public string Email { get; set; }
        public string Username { get; set; }
    }
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        private void SendEmail(string toEmail, string username)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Hệ Thống Thông Báo", "0917388156ab@gmail.com")); // Email của bạn
            message.To.Add(new MailboxAddress(username, toEmail));
            message.Subject = "Chào mừng bạn gia nhập!";

            message.Body = new TextPart("plain")
            {
                Text = $"Chào {username}, tài khoản của bạn đã được đăng ký thành công trên hệ thống!"
            };

            using (var client = new SmtpClient())
            {
                // 1. Kết nối với Server Gmail
                client.Connect("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);

                // 2. Xác thực bằng Mật khẩu ứng dụng (Dán mã 16 ký tự vào đây)
                // Lưu ý: Viết liền, không cần dấu cách
                client.Authenticate("0917388156ab@gmail.com", "czqh jhor nphv ujoz");
                _logger.LogInformation("==================", toEmail, username);
                // 3. Gửi và ngắt kết nối
                client.Send(message);
                client.Disconnect(true);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 1. Thiết lập kết nối
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: "new_user_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                _logger.LogInformation(" [x] Đã nhận tin nhắn: {0}", message);

                // GỌI HÀM GỬI MAIL Ở ĐÂY
                try
                {
                    var userData = JsonSerializer.Deserialize<UserMailTask>(message);
                    SendEmail(userData.Email, userData.Username);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Lỗi xử lý: " + ex.Message);
                }
            };

            channel.BasicConsume(queue: "new_user_queue", autoAck: true, consumer: consumer);

            // Giữ cho Worker không bị thoát
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
