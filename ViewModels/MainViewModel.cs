using System;
using EcoKoin.Models;
using EcoKoin.Services;

namespace EcoKoin.ViewModels
{
    public class MainViewModel
    {
        public User CurrentUser { get; set; }
        public PowerMonitorService PowerService { get; set; }
        public WeatherService WeatherService { get; set; }

        public void Initialize()
        {
            throw new NotImplementedException();
        }

        public void RefreshDashboard()
        {
            throw new NotImplementedException();
        }
    }
}
