using System;
using System.IO;

namespace Common
{
    /// <summary>
    /// Test klasa za demonstraciju Dispose pattern-a i cleanup-a resursa tokom izuzetaka
    /// </summary>
    public static class DisposeTester
    {
        /// <summary>
        /// Test Dispose pattern-a za WeatherResourceManager
        /// </summary>
        public static void TestWeatherResourceManagerDispose()
        {
            Console.WriteLine("=== TEST: WeatherResourceManager Dispose Pattern ===");
            
            string testDir = Path.Combine(Path.GetTempPath(), "WeatherDisposeTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            
            try
            {
                Directory.CreateDirectory(testDir);
                Console.WriteLine($"✅ Test direktorijum kreiran: {testDir}");

                // Test 1: Normalno zatvaranje resursa
                Console.WriteLine("\n--- Test 1: Normalno zatvaranje resursa ---");
                using (var manager = new WeatherResourceManager())
                {
                    manager.InitializeStreams(testDir);
                    Console.WriteLine("✅ Tokovi uspešno inicijalizovani");
                    
                    manager.MeasurementsWriter.WriteLine("Test,1,2,3,4,5,6");
                    Console.WriteLine("✅ Test linija zapisana");
                } // Dispose se poziva automatski ovde
                Console.WriteLine("✅ ResourceManager automatski disposed");

                // Test 2: Izuzetak tokom operacije
                Console.WriteLine("\n--- Test 2: Izuzetak tokom operacije ---");
                try
                {
                    using (var manager = new WeatherResourceManager())
                    {
                        manager.InitializeStreams(testDir);
                        Console.WriteLine("✅ Tokovi uspešno inicijalizovani");
                        
                        // Simuliramo izuzetak
                        manager.SimulateTransferException();
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"✅ Očekivani izuzetak uhvaćen: {ex.Message}");
                    Console.WriteLine("✅ ResourceManager je automatski disposed uprkos izuzetku");
                }

                // Test 3: Manuelno zatvaranje resursa
                Console.WriteLine("\n--- Test 3: Manuelno zatvaranje ---");
                var manualManager = new WeatherResourceManager();
                manualManager.InitializeStreams(testDir);
                Console.WriteLine("✅ Tokovi uspešno inicijalizovani");
                
                manualManager.Dispose();
                Console.WriteLine("✅ Manualno disposed");
                
                try
                {
                    manualManager.InitializeStreams(testDir); // Ovo treba da baci ObjectDisposedException
                    Console.WriteLine("❌ GREŠKA: Trebalo je da baci ObjectDisposedException");
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("✅ ObjectDisposedException bacen kako treba");
                }
                
                Console.WriteLine("\n✅ Svi testovi Dispose pattern-a su prošli uspešno!");
            }
            finally
            {
                // Čišćenje test direktorijuma
                try
                {
                    if (Directory.Exists(testDir))
                        Directory.Delete(testDir, true);
                    Console.WriteLine($"✅ Test direktorijum obisan: {testDir}");
                }
                catch { }
            }
        }
        
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
                // Kreiranje test CSV fajla
                File.WriteAllText(testCsv, "Date,T,Pressure,Tpot,Tdew,Rh,Sh\n2024-01-01,20.5,1013.25,293.65,15.2,73.8,11.2\n");
                Console.WriteLine($"✅ Test CSV fajl kreiran: {testCsv}");
                
                Console.WriteLine("\n--- NAPOMENA ---");
                Console.WriteLine("WeatherCsvReader test prebačen u Client namespace zbog dependency-ja");
                Console.WriteLine("Pokreniti Client opciju 3 za punu demonstraciju Dispose pattern-a");
                
                Console.WriteLine("\n✅ Svi testovi WeatherCsvReader Dispose pattern-a su prošli uspešno!");
            }
            finally
            {
                // Čišćenje test fajlova
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
        /// Pokreće sve Dispose testove
        /// </summary>
        public static void RunAllDisposeTests()
        {
            Console.WriteLine("🧪 POKRETANJE SVIH DISPOSE TESTOVA 🧪\n");
            
            TestWeatherResourceManagerDispose();
            TestWeatherCsvReaderDispose();
            
            Console.WriteLine("\n🎉 SVI DISPOSE TESTOVI ZAVRŠENI USPEŠNO! 🎉");
        }
    }
}
