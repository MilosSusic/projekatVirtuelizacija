using Common;
using System;
using System.Globalization;
using System.IO;

namespace Client
{
    /// <summary>
    /// CSV reader sa Dispose pattern implementacijom za čitanje meteoroloških podataka
    /// </summary>
    public class WeatherCsvReader : IDisposable
    {
        private readonly FileStream _fileStream;
        private readonly StreamReader _reader;
        private readonly FileStream _rejectsStream;
        private readonly StreamWriter _rejectsWriter;
        private bool _disposed = false;
        private bool _headerSkipped = false;

        public int AcceptedCount { get; private set; }
        public int RejectedCount { get; private set; }

        public WeatherCsvReader(string csvFilePath, string rejectsFilePath)
        {
            try
            {
                _fileStream = new FileStream(csvFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _reader = new StreamReader(_fileStream);


                // Create rejects file
                Directory.CreateDirectory(Path.GetDirectoryName(rejectsFilePath));
                _rejectsStream = new FileStream(rejectsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _rejectsWriter = new StreamWriter(_rejectsStream) { AutoFlush = true };
                _rejectsWriter.WriteLine("Error,Line");
            }
            catch (Exception ex)
            {
                // Cleanup on initialization failure
                Dispose();
                throw new InvalidOperationException($"Failed to initialize CSV reader: {ex.Message}", ex);
            }
        }

        public bool TryReadNext(out WeatherSample sample)
        {
            sample = null;

            if (_disposed)
                throw new ObjectDisposedException(nameof(WeatherCsvReader));

            try
            {
                string line;
                while ((line = _reader.ReadLine()) != null)
                {
                    // Skip header line
                    if (!_headerSkipped)
                    {
                        _headerSkipped = true;
                        if (line.ToLowerInvariant().Contains("date") || line.ToLowerInvariant().Contains("timestamp"))
                        {
                            continue; // Skip header
                        }
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (WeatherSample.TryParseCsv(line, out sample, out string error))
                    {
                        AcceptedCount++;
                        return true;
                    }
                    else
                    {
                        RejectedCount++;
                        _rejectsWriter?.WriteLine($"\"{error.Replace("\"", "\"\"")}\",\"{line.Replace("\"", "\"\"")}\"");
                        
                        // Continue to next line instead of returning false
                        // This allows processing to continue even with some bad lines
                        continue;
                    }
                }

                return false; // End of file
            }
            catch (Exception ex)
            {
                RejectedCount++;
                _rejectsWriter?.WriteLine($"\"Read error: {ex.Message.Replace("\"", "\"\"")}\",\"\"");
                return false;
            }
        }

        /// <summary>
        /// Test metoda za simulaciju izuzetka tokom čitanja
        /// </summary>
        public void SimulateReadException()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WeatherCsvReader));

            throw new InvalidOperationException("Simulated read interruption - testing resource cleanup");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    try { _reader?.Dispose(); } catch { }
                    try { _fileStream?.Dispose(); } catch { }
                    try { _rejectsWriter?.Flush(); _rejectsWriter?.Dispose(); } catch { }
                    try { _rejectsStream?.Dispose(); } catch { }
                }

                _disposed = true;
            }
        }

        ~WeatherCsvReader()
        {
            Dispose(false);
        }
    }
}
