using System;
using System.Threading.Tasks;

namespace WeatherTelegramBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var bot = new TelegramBot("6674580438:AAGr1yTr502LSbGcPpOGa75m3DJi6vxy_4o", "3b766e026bd27bd9b2e845ec40701b4e");
            await bot.StartAsync();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            bot.Stop();
        }
    }
}
