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

        public string Otp { get; set; } // Trường mới để nhận mã 6 số
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
                var messageStr = Encoding.UTF8.GetString(body);
                // GỌI HÀM GỬI MAIL Ở ĐÂY
                try
                {
                    // Giải mã JSON sang khuôn UserMailTask
                    var task = JsonSerializer.Deserialize<UserMailTask>(messageStr);

                    if (task != null)
                    {
                        _logger.LogInformation($"[Worker] Đang gửi mã OTP: {task.Otp} tới Email: {task.Email}");

                        // Gửi Mail
                        SendOtpEmail(task.Email, task.Username, task.Otp);

                        _logger.LogInformation("[Worker] Đã gửi mail thành công!");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Worker] Lỗi xử lý tin nhắn: {ex.Message}");
                }
            };

            channel.BasicConsume(queue: "new_user_queue", autoAck: true, consumer: consumer);

            // Giữ cho Worker không bị thoát
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private void SendOtpEmail(string toEmail, string username, string otp)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Hệ Thống Xác Thực", "0917388156ab@gmail.com"));
            message.To.Add(new MailboxAddress(username, toEmail));
            message.Subject = $"{otp} là mã xác nhận tài khoản của bạn";

            // Tạo nội dung HTML cho email
            var bodyBuilder = new BodyBuilder();
            bodyBuilder.HtmlBody = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px;'>
            <h2 style='color: #007bff;'>Chào mừng {username}!</h2>
            <p>Cảm ơn bạn đã đăng ký tài khoản. Để hoàn tất việc kích hoạt, vui lòng sử dụng mã OTP dưới đây:</p>
            <div style='background: #f4f4f4; padding: 15px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 5px; color: #333;'>
                {otp}
            </div>
            <p style='margin-top: 20px;'>Mã này có hiệu lực trong vòng 5 phút. Nếu không phải bạn thực hiện yêu cầu này, hãy bỏ qua email này.</p>
            <hr>
            <small style='color: #888;'>Đây là email tự động, vui lòng không phản hồi.</small>
        </div>";

            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                client.Connect("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                client.Authenticate("0917388156ab@gmail.com", "czqh jhor nphv ujoz");
                client.Send(message);
                client.Disconnect(true);
            }
        }
    }
}
