using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WeatherTelegramBot
{
    public class WeatherService
    {
        private readonly string apiKey;
        private readonly HttpClient httpClient;

        public WeatherService(string apiKey)
        {
            this.apiKey = apiKey;
            this.httpClient = new HttpClient();
        }

        public async Task<string> GetWeather(string city)
        {
            try
            {
                var response = await httpClient.GetStringAsync($"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric&lang=ua");
                var weatherResponse = JsonConvert.DeserializeObject<WeatherResponse>(response);

                if (weatherResponse != null)
                {
                    var temperature = weatherResponse.Main.Temp;
                    var description = weatherResponse.Weather[0].Description;
                    return $"{temperature}°C, {description}";
                }

                return "Не вдалося отримати погоду для даного міста.";
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP Request Exception: {ex.Message}");
                return "Сталася помилка при отриманні погоди. Перевірте підключення до Інтернету або спробуйте пізніше.";
            }
            catch (JsonSerializationException ex)
            {
                Console.WriteLine($"JSON Serialization Exception: {ex.Message}");
                return "Сталася помилка при обробці відповіді з сервера погоди. Спробуйте пізніше.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return "Сталася невідома помилка при отриманні погоди. Спробуйте пізніше або зверніться до адміністратора.";
            }
        }
    }

    public class WeatherResponse
    {
        public MainInfo Main { get; set; }
        public WeatherInfo[] Weather { get; set; }
    }

    public class MainInfo
    {
        public float Temp { get; set; }
    }

    public class WeatherInfo
    {
        public string Description { get; set; }
    }
}
