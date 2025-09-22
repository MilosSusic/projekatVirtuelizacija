using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.Configuration;

namespace Client
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== METEOROLOŠKA STANICA KLIJENT ===");
            Console.WriteLine("Izaberite opciju:");
            Console.WriteLine("1. Meteorološka stanica");
            Console.WriteLine("2. Test Dispose Pattern-a");
            Console.WriteLine("3. Izlaz");
            Console.Write("Opcija (1-3): ");
            
            string choice = Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    WeatherClient.RunWeatherDemo();
                    break;
                case "2":
                    WeatherDisposeTester.RunAllDisposeTests();
                    break;
                case "3":
                    return;
                default:
                    Console.WriteLine("Nevalidna opcija. Pokretam Weather demo...");
                    WeatherClient.RunWeatherDemo();
                    break;
            }
            
            Console.WriteLine("\nPritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
        }
    }
}
