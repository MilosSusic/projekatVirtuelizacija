using Common;
using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;

namespace Client
{
    public class WeatherClient
    {
        public static void RunWeatherDemo()
        {
            ChannelFactory<IWeatherService> weatherFactory = null;
            IWeatherService weatherProxy = null;
            
            try
            {
                // Kreiranje kanala za povezivanje sa WCF servisom
                weatherFactory = new ChannelFactory<IWeatherService>("Weather");
                weatherProxy = weatherFactory.CreateChannel();
                
                Console.WriteLine("Testiranje konekcije sa Meteorološkim servisom...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Neuspešno povezivanje sa Meteorološkim servisom: {ex.Message}");
                Console.WriteLine("Proverite da li je Server pokrenut na localhost:4101");
                Console.ReadKey();
                return;
            }

            // Demo čitanja i slanja podataka
            Console.WriteLine("Unesite putanju do CSV fajla sa meteorološkim podacima (Enter = ./Client/Dataset/cleaned_weather.csv):");
            string path = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(path))
            {
                // Pokušaj da pronađe fajl u bin i projekt direktorijumu
                string binDataset = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dataset", "cleaned_weather.csv");
                string projDataset = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Client", "Dataset", "cleaned_weather.csv");
                path = File.Exists(binDataset) ? binDataset : projDataset;

                if (!File.Exists(path))
                {
                    // Pokušaj da pronađe bilo koji CSV fajl u Dataset direktorijumima
                    string binDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dataset");
                    string projDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Client", "Dataset");
                    string found = null;
                    
                    if (Directory.Exists(binDir))
                    {
                        var files = Directory.GetFiles(binDir, "*.csv")
                            .Where(f => !Path.GetFileName(f).StartsWith("rejects_"))     //izbacuje sve one koji su odbaceni
                            .ToArray();
                        if (files.Length > 0) found = files[0];
                    }
                    if (found == null && Directory.Exists(projDir))           //da vidimo jesu li u projektu
                    {
                        var files = Directory.GetFiles(projDir, "*.csv")                   
                            .Where(f => !Path.GetFileName(f).StartsWith("rejects_"))     //isto filtriramo one sa rejects
                            .ToArray();
                        if (files.Length > 0) found = files[0];
                    }
                    if (found != null)
                    {
                        path = found;
                    }
                }
            }

            // Validacija da fajl postoji
            while (!File.Exists(path))
            {
                Console.WriteLine("CSV fajl nije pronađen. Unesite punu putanju do .csv fajla:");
                path = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(path))
                {
                    Console.WriteLine("Putanja je prazna. Pokušajte ponovo.");
                    continue;
                }
            }

            // Kreiranje metapodataka sesije
            var meta = new WeatherSessionMeta
            {
                SessionId = Guid.NewGuid().ToString("N"),
                StartedAt = DateTime.UtcNow,
                Date = DateTime.UtcNow,
                TThreshold = double.Parse(ConfigurationManager.AppSettings["T_threshold"] ?? "2.0", CultureInfo.InvariantCulture),
                RHThreshold = double.Parse(ConfigurationManager.AppSettings["RH_threshold"] ?? "10.0", CultureInfo.InvariantCulture),
                DEWThreshold = double.Parse(ConfigurationManager.AppSettings["DEW_threshold"] ?? "1.5", CultureInfo.InvariantCulture),
                DeviationPercent = double.Parse(ConfigurationManager.AppSettings["DeviationPercent"] ?? "25", CultureInfo.InvariantCulture)
            };

            try
            {
                var ack = weatherProxy.StartSession(meta);
                Console.WriteLine($"Meteorološka sesija: {ack.Status}");
                if (!ack.Success)
                {
                    Console.WriteLine($"Greška: {ack.Message}");
                    return;
                }

                int sent = 0;
                int successful = 0;
                int failed = 0;
                Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dataset"));
                string rejects = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dataset", $"rejects_weather_client_{meta.SessionId}.csv");
                
                Console.WriteLine($"Čitanje iz: {path}");
                Console.WriteLine($"Fajl postoji: {File.Exists(path)}");
                Console.WriteLine("Šaljanje meteoroloških uzoraka...");
                Console.WriteLine($"Threshold vrednosti: T_threshold={meta.TThreshold}°C, RH_threshold={meta.RHThreshold}%, DEW_threshold={meta.DEWThreshold}°C, Odstupanje={meta.DeviationPercent}%");
                Console.WriteLine("Pratite ALARME u Server konzoli! 🚨");
                
                using (var reader = new WeatherCsvReader(path, rejects))
                {
                    while (sent < 100 && reader.TryReadNext(out var sample))
                    {
                        var resp = weatherProxy.PushSample(sample);  //server vraca za svaki red
                        sent++;
                        if (resp.Success)
                            successful++;
                        else
                        {
                            failed++;
                            if (failed <= 3)
                            {
                                Console.WriteLine($"\n❌ Klijent primio neuspeh: {resp.Message}");
                            }
                        }
                        
                        if (sent % 10 == 0)
                            Console.Write($"\rPoslato: {sent}, Uspešno: {successful}, Neuspešno: {failed}");
                    }
                    Console.WriteLine($"\nPrihvaćeno={reader.AcceptedCount} Odbačeno={reader.RejectedCount}");
                }
                
                var end = weatherProxy.EndSession();  //zavrsetak sesije
                Console.WriteLine($"\nMetorološka sesija završena: {end.Status}");
                Console.WriteLine($"Ukupno poslato: {sent}, Uspešno: {successful}, Neuspešno: {failed}");
                if (sent == 0)
                {
                    Console.WriteLine("Nije poslat nijedan uzorak. Proverite format CSV-a ili putanju.");
                }
            }
            catch (FaultException<CustomException> ex)
            {
                Console.WriteLine($"Meteorološka GREŠKA: {ex.Detail.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Neočekivana greška: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (weatherProxy is ICommunicationObject commObj)
                        commObj.Close();    // zatvaranje komunikacije sa serverom
                    weatherFactory?.Close();    // zatvaranje kanala
                }
                catch { }
            }
        }
    }
}
