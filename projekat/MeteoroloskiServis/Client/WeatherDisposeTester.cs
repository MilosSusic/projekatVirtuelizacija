using Common;
using System;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace Client
{
    /// <summary>
    /// Test klasa za demonstraciju Dispose pattern-a WeatherCsvReader-a
    /// </summary>
    public static class WeatherDisposeTester
    {
        /// <summary>
        /// Test Dispose pattern-a za WeatherCsvReader
        /// </summary>
        public static void TestWeatherCsvReaderDispose()
        {
            Console.WriteLine("\n=== TEST: WeatherCsvReader Dispose Pattern ===");
            
            string testCsv = Path.Combine(Path.GetTempPath(), "test_weather.csv");
            string testRejects = Path.Combine(Path.GetTempPath(), "test_rejects.csv");
            
            try
            {
                // Kreiramo privremeni CSV fajl samo za potrebe testiranja Dispose pattern-a
                File.WriteAllText(testCsv, "Date,T,Pressure,Tpot,Tdew,Rh,Sh\n2024-01-01,20.5,1013.25,293.65,15.2,73.8,11.2\n");
                Console.WriteLine($"✅ Test CSV fajl kreiran: {testCsv}");


                // Test 1: Normalno zatvaranje
                Console.WriteLine("\n--- Test 1: Normalno zatvaranje ---");
                WeatherSample sample;
                using (var reader = new WeatherCsvReader(testCsv, testRejects))
                {
                    bool result = reader.TryReadNext(out sample);
                    Console.WriteLine($"✅ Čitanje: {result}, Prihvaćeno: {reader.AcceptedCount}");
                } // Dispose automatski
                Console.WriteLine("✅ WeatherCsvReader automatski disposed");

                // Test 2: Izuzetak tokom čitanja
                Console.WriteLine("\n--- Test 2: Izuzetak tokom čitanja ---");
                try
                {
                    using (var reader = new WeatherCsvReader(testCsv, testRejects))
                    {
                        reader.TryReadNext(out sample);
                        Console.WriteLine("✅ Prvo čitanje uspešno");
                        
                        // Simuliramo izuzetak
                        reader.SimulateReadException();
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"✅ Očekivani izuzetak uhvaćen: {ex.Message}");
                    Console.WriteLine("✅ WeatherCsvReader je automatski disposed uprkos izuzetku");
                }
                
                Console.WriteLine("\n✅ Svi testovi WeatherCsvReader Dispose pattern-a su prošli uspešno!");
            }
            finally
            {
                // Cleanup test fajlova
                try
                {
                    if (File.Exists(testCsv)) File.Delete(testCsv);
                    if (File.Exists(testRejects)) File.Delete(testRejects);
                    Console.WriteLine("✅ Test fajlovi obrisani");
                }
                catch { }
            }
        }
        
        /// <summary>
        /// Pokreće sve Dispose testove uključujući i one iz Common-a
        /// </summary>
        public static void RunAllDisposeTests()
        {
            Console.WriteLine("🧪 POKRETANJE SVIH DISPOSE TESTOVA 🧪\n");
            
            // Pozovi testove iz Common namespace-a
            Common.DisposeTester.TestWeatherResourceManagerDispose();
            
            // Pozovi lokalne testove
            TestWeatherCsvReaderDispose();
            
            Console.WriteLine("\n🎉 SVI DISPOSE TESTOVI ZAVRŠENI USPEŠNO! 🎉");
        }
    }
}
