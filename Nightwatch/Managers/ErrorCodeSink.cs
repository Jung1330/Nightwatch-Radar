using System;
using System.IO;
using System.Text;

namespace Nightwatch
{
    public static class ErrorCodeSink
    {
        private static readonly object _sync = new object();
        private static bool _installed;

        public static void Install()
        {
            lock (_sync)
            {
                if (_installed) return;

                var outWriter = Console.Out;
                var errWriter = Console.Error;
                var mirrorWriter = new ErrorCodeMirrorTextWriter(outWriter, errWriter);
                Console.SetOut(mirrorWriter);
                Console.SetError(mirrorWriter);

                _installed = true;
            }
        }

        private sealed class ErrorCodeMirrorTextWriter : TextWriter
        {
            private readonly TextWriter _out;
            private readonly TextWriter _err;
            private static readonly object _logFileLock = new object();

            public ErrorCodeMirrorTextWriter(TextWriter outWriter, TextWriter errWriter)
            {
                _out = outWriter;
                _err = errWriter;
            }

            public override Encoding Encoding => _out.Encoding;

            public override void WriteLine(string value)
            {
                _out.WriteLine(value);
                _out.Flush();
                MirrorErrorCode(value);
            }

            public override void WriteLine(object value)
            {
                WriteLine(value?.ToString());
            }

            public override void Write(string value)
            {
                _out.Write(value);
            }

            private static void MirrorErrorCode(string line)
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                if (!line.Contains("Error Code :", StringComparison.OrdinalIgnoreCase)) return;

                try
                {
                    string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Logs");
                    Directory.CreateDirectory(logDir);
                    string logPath = Path.Combine(logDir, "error_codes.log");
                    string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}";

                    lock (_logFileLock)
                    {
                        File.AppendAllText(logPath, entry, Encoding.UTF8);
                    }

                    UIConsole.Log(line, LogLevel.Error);
                }
                catch
                {
                    // Sink fail etse de uygulama akýþýný bozmayalým.
                }
            }
        }
    }
}


