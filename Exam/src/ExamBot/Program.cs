using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ExamBot;

internal class Program
{
    static void Main(string[] args)
    {
        string botToken = "8241676564:AAEnO1XcBwYo02OZVHNBwZCdx9nMIXywEZM";
        string usersFile = Path.Combine(AppContext.BaseDirectory, "users.json");
        string photosDirectory = Path.Combine(AppContext.BaseDirectory, "downloaded_photos");

        if (!Directory.Exists(photosDirectory)) Directory.CreateDirectory(photosDirectory);
        if (!File.Exists(usersFile)) File.WriteAllText(usersFile, "{}");

        var botClient = new TelegramBotClient(botToken);

        Dictionary<string, string> LoadUsers()
        {
            var json = File.ReadAllText(usersFile);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        void SaveUsers(Dictionary<string, string> users)
        {
            var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(usersFile, json);
        }

        async Task HandleUpdateAsync(ITelegramBotClient client, Update update, CancellationToken ct)
        {
            try
            {
                if (update.Type == UpdateType.Message && update.Message?.Type == MessageType.Text)
                {
                    var message = update.Message;
                    var userId = message.From?.Id.ToString() ?? "";
                    var userName = message.From?.Username ?? message.From?.FirstName ?? "Unknown";
                    var text = message.Text ?? "";

                    if (text == "/start")
                    {
                        var users = LoadUsers();
                        if (!users.ContainsKey(userId))
                        {
                            users[userId] = userName;
                            SaveUsers(users);
                        }

                        await client.SendMessage(
                            message.Chat.Id,
                            $"Ассаляму Алейкум бро, {userName} \nТы Зарегистрирован!",
                            cancellationToken: ct);
                    }
                    else if (!string.IsNullOrWhiteSpace(text) && text != "/start")
                    {
                        var users = LoadUsers();
                        if (!users.ContainsKey(userId))
                        {
                            users[userId] = userName;
                            SaveUsers(users);
                        }

                        await client.SendMessage(
                            message.Chat.Id,
                            "Бро я ищу твои фотографии жди сабр сабр ",
                            cancellationToken: ct);

                        var photoUrls = await SearchPhotos(text);
                        if (photoUrls.Count == 0)
                        {
                            await client.SendMessage(
                                message.Chat.Id,
                                $"Фотографии для '{text}' не найдены, бро извини",
                                cancellationToken: ct);
                            return;
                        }

                        var downloadedPhotos = new List<string>();
                        foreach (var url in photoUrls.Take(3))
                        {
                            try
                            {
                                var photoPath = await DownloadPhoto(url, text);
                                if (!string.IsNullOrEmpty(photoPath))
                                {
                                    downloadedPhotos.Add(photoPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка при скачивание бро повтори ещё разок {url}: {ex.Message}");
                            }
                        }
                        if (downloadedPhotos.Count > 0)
                        {
                            foreach (var photoPath in downloadedPhotos)
                            {
                                using (var stream = new FileStream(photoPath, FileMode.Open))
                                {
                                    await client.SendPhoto(
                                        message.Chat.Id,
                                        new InputFileStream(stream, Path.GetFileName(photoPath)),
                                        cancellationToken: ct);
                                }
                            }
                            await client.SendMessage(
                                message.Chat.Id,
                                $"Нашел бро {downloadedPhotos.Count} фотографии для '{text}' доволен",
                                cancellationToken: ct);
                        }
                        else
                        {
                            await client.SendMessage(
                                message.Chat.Id,
                                "Не получилось бро",
                                cancellationToken: ct);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        async Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken ct)
        {
            Console.WriteLine($"Ошибка телеграм бота: {exception.Message}");
        }

        async Task<List<string>> SearchPhotos(string query)
        {
            var urls = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                var randomSeed = new Random().Next(1000, 9999);
                urls.Add($"https://picsum.photos/600/400?random={randomSeed}");
            }

            return urls;
        }

        async Task<string> DownloadPhoto(string photoUrl, string query)
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    var response = await httpClient.GetAsync(photoUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var fileName = $"{query}_{DateTime.Now.Ticks}.jpg";
                        var filePath = Path.Combine(photosDirectory, fileName);

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await stream.CopyToAsync(fileStream);
                        }

                        return filePath;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при скачивании фотографии повтори ещё разок: {ex.Message}");
                }
            }
            return "";
        }

        using (var cts = new CancellationTokenSource())
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message }
            };

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token);

            Console.WriteLine("Бот работает");
            Console.ReadLine();
            cts.Cancel();
        }
    }
}