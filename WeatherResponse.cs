﻿namespace WeatherTelegramBot
{
    public class WeatherResponseB
    {
        public MainInfo Main { get; set; }
        public WeatherInfo[] Weather { get; set; }

        public class MainInfo
        {
            public float Temp { get; set; }
        }

        public class WeatherInfo
        {
            public string Description { get; set; }
        }
    }
}
