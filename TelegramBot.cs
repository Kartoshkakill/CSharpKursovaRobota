using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace WeatherTelegramBot
{
    public class TelegramBot
    {
        private readonly TelegramBotClient botClient;
        private readonly WeatherService weatherService;
        private readonly Dictionary<long, string> userCities = new Dictionary<long, string>();
        private readonly HashSet<long> subscribedUsers = new HashSet<long>();

        public TelegramBot(string token, string weatherApiKey)
        {
            botClient = new TelegramBotClient(token);
            weatherService = new WeatherService(weatherApiKey);
        }

        public async Task StartAsync()
        {
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );

            Console.WriteLine("Бот запущено...");
            await Task.Run(() => ScheduleDailyWeatherReports(cancellationToken));
        }

        public void Stop()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Type == UpdateType.Message)
                {
                    var message = update.Message;
                    if (message.Type == MessageType.Text)
                    {
                        if (message.Text.StartsWith("/start"))
                        {
                            await SendCitySelectionMessage(message.Chat.Id);
                        }
                        else if (message.Text.StartsWith("/change_city"))
                        {
                            await SendCitySelectionMessage(message.Chat.Id);
                            await botClient.SendTextMessageAsync(message.Chat.Id, "Введіть нову назву міста або оберіть нижче.");
                        }
                        else if (!string.IsNullOrWhiteSpace(message.Text))
                        {
                            var city = message.Text.Trim();
                            var weather = await weatherService.GetWeather(city);
                            if (!string.IsNullOrEmpty(weather))
                            {
                                userCities[message.Chat.Id] = city;
                                await botClient.SendTextMessageAsync(message.Chat.Id, $"Ви обрали місто: {city}. Для зміни міста використовуйте /change_city.");
                                await ShowWeatherOptions(message.Chat.Id);
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, "Такого міста немає в моїй базі або ви припустили помилку при введенні. Спробуйте знову.");
                            }
                        }
                    }
                }
                else if (update.Type == UpdateType.CallbackQuery)
                {
                    var callbackQuery = update.CallbackQuery;
                    if (callbackQuery.Data == "weather_now")
                    {
                        if (userCities.TryGetValue(callbackQuery.Message.Chat.Id, out string city))
                        {
                            var weather = await weatherService.GetWeather(city);
                            if (!string.IsNullOrEmpty(weather))
                            {
                                var recommendation = GetWeatherRecommendation(weather);
                                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"{weather}\n{recommendation}");
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Сталася помилка при отриманні погоди. Спробуйте ще раз.");
                            }
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Спочатку оберіть місто!");
                        }
                    }
                    else if (callbackQuery.Data == "subscribe")
                    {
                        subscribedUsers.Add(callbackQuery.Message.Chat.Id);
                        if (userCities.TryGetValue(callbackQuery.Message.Chat.Id, out string city))
                        {
                            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Ви _підписались_ на розсилку прогнозу погоди міста {city}. Ви отримуватимете повідомлення щодня о 6:00", parseMode: ParseMode.Markdown);
                        }
                    }
                    else if (callbackQuery.Data == "unsubscribe")
                    {
                        subscribedUsers.Remove(callbackQuery.Message.Chat.Id);
                        await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Ви відписались від розсилки", parseMode: ParseMode.Markdown);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка обробки оновлення: {ex.Message}");
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Помилка Telegram API:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }

        private async Task SendCitySelectionMessage(long chatId)
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Київ", "Львів" },
                new KeyboardButton[] { "Одеса", "Харків" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };
            await botClient.SendTextMessageAsync(chatId, "Оберіть запропоноване місто або введіть інше:", replyMarkup: keyboard);
        }

        private async Task ShowWeatherOptions(long chatId)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Переглянути прогноз", "weather_now") },
                new[] { InlineKeyboardButton.WithCallbackData("Підписатись на прогноз міста", "subscribe") },
                new[] { InlineKeyboardButton.WithCallbackData("Скасувати підписку", "unsubscribe") }
            });
            await botClient.SendTextMessageAsync(chatId, "Оберіть опцію:", replyMarkup: keyboard);
        }

        private async Task ScheduleDailyWeatherReports(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRunTime = new DateTime(now.Year, now.Month, now.Day, 21, 0, 0);
                if (now > nextRunTime)
                    nextRunTime = nextRunTime.AddDays(1);
                var delay = nextRunTime - now;
                await Task.Delay(delay, cancellationToken);

                foreach (var userId in subscribedUsers)
                {
                    if (userCities.TryGetValue(userId, out string city))
                    {
                        var weather = await weatherService.GetWeather(city);
                        if (!string.IsNullOrEmpty(weather))
                        {
                            var recommendation = GetWeatherRecommendation(weather);
                            await botClient.SendTextMessageAsync(userId, $"Щоденний прогноз погоди міста {city}: {weather}\n{recommendation}");
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(userId, "Сталася помилка при отриманні погоди для розсилки. Спробуйте ще раз.");
                        }
                    }
                }
            }
        }

        private string GetWeatherRecommendation(string weather)
        {
            if (weather.Contains("rain"))
            {
                return "Не забудьте взяти парасольку!";
            }
            else if (weather.Contains("clear"))
            {
                return "Сьогодні гарна погода, насолоджуйтесь сонцем!";
            }
            else if (weather.Contains("clouds"))
            {
                return "Можливо, знадобиться легка куртка.";
            }
            else if (weather.Contains("snow"))
            {
                return "Одягайтесь тепло, на вулиці сніг!";
            }
            else
            {
                return "Сьогодні погода неочікувана, будьте готові до всього!";
            }
        }
    }
}
