using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Create and open the WCF host for WeatherService
                using (ServiceHost weatherHost = new ServiceHost(typeof(WeatherService)))
                {
                    weatherHost.Open();

                    Console.WriteLine("=== METEOROLOŠKA STANICA SERVIS ===");
                    Console.WriteLine($"Meteorološki servis je pokrenut na: {weatherHost.BaseAddresses[0]}");
                    Console.WriteLine("Čekaju se klijentske konekcije...");
                    Console.WriteLine("Pritisnite bilo koji taster da zaustavite servis");
                    Console.WriteLine("====================================");
                    
                    Console.ReadKey();

                    weatherHost.Close();
                }

                Console.WriteLine("Servis je zatvoren");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting service: {ex.Message}");
                Console.WriteLine("Make sure port 4100 is not in use by another application");
            }
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
