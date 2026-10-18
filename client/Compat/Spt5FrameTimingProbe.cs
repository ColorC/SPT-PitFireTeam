using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace pitTeam.Compat
{
    // Opt-in observation only. One Unity-thread producer; one managed writer.
    // The steady-state producer has no formatting, locks, file I/O or allocations.
    internal static class Spt5FrameTimingProbe
    {
        private static Capture? _capture;
        private static int _attempted;
        private static int _disposed;
        private static string? _initializationError;

        internal static void Tick()
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            Capture? capture = Volatile.Read(ref _capture);
            if (capture == null)
            {
                if (Interlocked.CompareExchange(ref _attempted, 1, 0) != 0) return;
                try
                {
                    string? root = GetEnabledRoot();
                    if (root == null) return;
                    ManagedFramePump.RequireOwnerThread();
                    capture = new Capture(root);
                    capture.Start();
                    Volatile.Write(ref _capture, capture);
                    AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                }
                catch (Exception error) { _initializationError = Describe(error); return; }
            }
            capture.Tick();
        }

        // Request a writer-side flush; never wait for a disk write on the game thread.
        internal static void Flush() => Volatile.Read(ref _capture)?.RequestFlush();

        // Stop accepting samples and let the background writer drain. Do not join here.
        internal static void Dispose()
        {
            Interlocked.Exchange(ref _disposed, 1);
            Volatile.Read(ref _capture)?.Stop();
        }

        internal static object Snapshot() => Volatile.Read(ref _capture)?.Snapshot()
            ?? new { enabled = false, attempted = Volatile.Read(ref _attempted) != 0,
                disposed = Volatile.Read(ref _disposed) != 0, initializationError = _initializationError };

        private static void OnProcessExit(object? sender, EventArgs args)
        {
            Dispose();
            // No native/Unity access here. A hard kill can still leave a partial CSV.
            Volatile.Read(ref _capture)?.WaitForWriter(1000);
        }

        private static string? GetEnabledRoot()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_SHADOW_MODE"), "true", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_CAPTURE_RUNTIME_MEMORY"), "true", StringComparison.OrdinalIgnoreCase)
                || !Uri.TryCreate(Environment.GetEnvironmentVariable("PIT_SPT5_BACKEND_URL"), UriKind.Absolute, out Uri? backend)
                || backend.Scheme != Uri.UriSchemeHttps || backend.Host != "127.0.0.1" || backend.Port != 6971
                || backend.UserInfo.Length != 0 || backend.Query.Length != 0 || backend.Fragment.Length != 0
                || backend.AbsolutePath != "/") return null;
            string? value = Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT");
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value)) return null;
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
            if (root == Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root)!) || !Directory.Exists(root)
                || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return null;
            return root;
        }

        private static string Describe(Exception error) => error.GetType().Name + ": " + error.Message;

        private struct Sample
        {
            internal long Sequence, Timestamp, Interval, Duration, NativeUsed, NativeHeap, Dropped, Errors;
            internal int Frame, Gen0, Gen1, Gen2, ErrorFlags;
        }

        private sealed class Capture
        {
            private const int Capacity = 4096; // at most 4095 queued samples, under 0.5 MiB
            private readonly Sample[] _ring = new Sample[Capacity];
            private readonly AutoResetEvent _wake = new(false);
            private readonly ManualResetEventSlim _completed = new(false);
            private readonly string _root;
            private readonly int _ownerThread = Environment.CurrentManagedThreadId;
            private readonly Thread _writer;
            private readonly long _anchorUtcTicks, _anchorTimestamp, _anchorUncertaintyTicks;
            private int _write, _read, _stop, _flush, _writerStopped, _producerInFlight, _lastFrame = int.MinValue;
            private long _sequence, _lastTimestamp, _dropped, _errors, _written;
            private string? _samplingError, _writerError;

            internal Capture(string root)
            {
                _root = root;
                // Bound the wall-clock observation by two QPC reads. The midpoint
                // maps Stopwatch ticks to UTC; uncertainty is recorded, not hidden.
                long before = Stopwatch.GetTimestamp();
                _anchorUtcTicks = DateTime.UtcNow.Ticks;
                long after = Stopwatch.GetTimestamp();
                _anchorTimestamp = before + (after - before) / 2;
                _anchorUncertaintyTicks = after - before;
                _writer = new Thread(WriteLoop) { IsBackground = true, Name = "PFT frame timing writer" };
            }

            internal void Start() => _writer.Start();
            internal void RequestFlush() { Interlocked.Exchange(ref _flush, 1); _wake.Set(); }
            internal void Stop() { Volatile.Write(ref _stop, 1); _wake.Set(); }
            internal void WaitForWriter(int milliseconds) => _completed.Wait(milliseconds);

            internal void Tick()
            {
                if (Volatile.Read(ref _stop) != 0 || Volatile.Read(ref _writerStopped) != 0) return;
                if (Environment.CurrentManagedThreadId != _ownerThread)
                {
                    Interlocked.Increment(ref _errors);
                    _samplingError = "Tick called from a different managed thread; no Unity/native reads were issued.";
                    Stop();
                    return;
                }
                Volatile.Write(ref _producerInFlight, 1);
                try
                {
                    if (Volatile.Read(ref _stop) == 0) CaptureOneFrame();
                }
                finally
                {
                    Volatile.Write(ref _producerInFlight, 0);
                    if (Volatile.Read(ref _stop) != 0) _wake.Set();
                }
            }

            private void CaptureOneFrame()
            {
                Sample sample = default;
                sample.Timestamp = Stopwatch.GetTimestamp();
                sample.NativeUsed = sample.NativeHeap = -1;
                sample.Frame = -1;
                int stage = 1;
                bool failed = false;
                try
                {
                    sample.Frame = Time.frameCount;
                    if (sample.Frame == _lastFrame) return; // duplicate host calls are not new frames
                    _lastFrame = sample.Frame;
                    stage = 2; sample.NativeUsed = IL2CPP.il2cpp_gc_get_used_size();
                    stage = 4; sample.NativeHeap = IL2CPP.il2cpp_gc_get_heap_size();
                    stage = 8;
                    sample.Gen0 = GC.CollectionCount(0);
                    sample.Gen1 = GC.CollectionCount(1);
                    sample.Gen2 = GC.CollectionCount(2);
                }
                catch (Exception error)
                {
                    sample.ErrorFlags = stage;
                    Interlocked.Increment(ref _errors);
                    _samplingError = Describe(error);
                    failed = true; // fail once, then stop; do not throw/log on every AI frame
                }
                sample.Duration = Stopwatch.GetTimestamp() - sample.Timestamp;
                sample.Sequence = ++_sequence;
                sample.Interval = _lastTimestamp == 0 ? 0 : sample.Timestamp - _lastTimestamp;
                _lastTimestamp = sample.Timestamp;
                sample.Errors = Interlocked.Read(ref _errors);
                int head = _write;
                int next = (head + 1) & (Capacity - 1);
                if (next == Volatile.Read(ref _read)) Interlocked.Increment(ref _dropped);
                else
                {
                    sample.Dropped = Interlocked.Read(ref _dropped);
                    _ring[head] = sample;
                    Volatile.Write(ref _write, next);
                    if ((sample.Sequence & 63) == 0) _wake.Set();
                }
                if (failed) Stop();
            }

            internal object Snapshot() => new
            {
                enabled = true, ownerThread = _ownerThread,
                capturedFrames = Interlocked.Read(ref _sequence), writtenSamples = Interlocked.Read(ref _written),
                droppedSamples = Interlocked.Read(ref _dropped), samplingErrors = Interlocked.Read(ref _errors),
                samplingError = _samplingError, writerError = _writerError,
                stopRequested = Volatile.Read(ref _stop) != 0, writerStopped = Volatile.Read(ref _writerStopped) != 0,
                capacity = Capacity - 1, path = Path.Combine(_root, "frames.csv")
            };

            private void WriteLoop()
            {
                try
                {
                    string csvPath = Path.Combine(_root, "frames.csv");
                    string clockPath = Path.Combine(_root, "frames-clock.json");
                    string summaryPath = Path.Combine(_root, "frames-summary.json");
                    if (File.Exists(csvPath) || File.Exists(clockPath) || File.Exists(summaryPath))
                        throw new IOException("Frame capture outputs already exist; refusing to append or overwrite.");
                    using var csv = new FileStream(csvPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536);
                    using var summary = new FileStream(summaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    using (var clock = new FileStream(clockPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                        JsonSerializer.Serialize(clock, new
                        {
                            schemaVersion = 1, pid = Environment.ProcessId, stopwatchFrequency = Stopwatch.Frequency,
                            anchorUtcTicks = _anchorUtcTicks,
                            anchorUtc = new DateTime(_anchorUtcTicks, DateTimeKind.Utc),
                            anchorStopwatchTicks = _anchorTimestamp, anchorReadSpanStopwatchTicks = _anchorUncertaintyTicks,
                            intervalMeaning = "elapsed between successive samples at the host Update callback; not a presentation/frame-time measurement",
                            utcMeaning = "fixed UTC/QPC mapping; later wall-clock adjustments do not change this timeline",
                            errorFlags = new { frameRead = 1, il2cppUsedRead = 2, il2cppHeapRead = 4, managedCollectionRead = 8 }
                        });
                    byte[] header = Encoding.ASCII.GetBytes("sequence,frame,stopwatch_ticks,utc_ticks,frame_interval_ticks,sample_duration_ticks,il2cpp_used_bytes,il2cpp_heap_bytes,managed_gen0,managed_gen1,managed_gen2,dropped_total,sampling_errors_total,error_flags\n");
                    csv.Write(header, 0, header.Length);
                    byte[] row = new byte[512];
                    long lastFlush = Stopwatch.GetTimestamp();
                    while (true)
                    {
                        int tail = _read;
                        while (tail != Volatile.Read(ref _write))
                        {
                            Sample sample = _ring[tail];
                            int length = FormatRow(row, sample);
                            csv.Write(row, 0, length);
                            Interlocked.Increment(ref _written);
                            tail = (tail + 1) & (Capacity - 1);
                            Volatile.Write(ref _read, tail);
                        }
                        long now = Stopwatch.GetTimestamp();
                        bool stop = Volatile.Read(ref _stop) != 0;
                        if (stop || Interlocked.Exchange(ref _flush, 0) != 0 || now - lastFlush >= Stopwatch.Frequency)
                        {
                            csv.Flush();
                            WriteSummary(summary, stop);
                            lastFlush = now;
                        }
                        if (stop && Volatile.Read(ref _producerInFlight) == 0 && tail == Volatile.Read(ref _write)) break;
                        _wake.WaitOne(200);
                    }
                }
                catch (Exception error) { _writerError = Describe(error); }
                finally { Volatile.Write(ref _writerStopped, 1); _completed.Set(); }
            }

            private void WriteSummary(FileStream output, bool stopped)
            {
                output.Position = 0;
                output.SetLength(0);
                JsonSerializer.Serialize(output, new
                {
                    schemaVersion = 1, stopped,
                    capturedFrames = Interlocked.Read(ref _sequence), writtenSamples = Interlocked.Read(ref _written),
                    droppedSamples = Interlocked.Read(ref _dropped), samplingErrors = Interlocked.Read(ref _errors),
                    samplingError = _samplingError, writerError = _writerError,
                    bufferCapacity = Capacity - 1, savedUtc = DateTime.UtcNow
                });
                output.Flush();
            }

            private int FormatRow(byte[] buffer, Sample sample)
            {
                Span<byte> row = buffer;
                int offset = 0;
                long delta = sample.Timestamp - _anchorTimestamp;
                long utc = _anchorUtcTicks + (long)(delta * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
                Number(row, ref offset, sample.Sequence); Number(row, ref offset, sample.Frame);
                Number(row, ref offset, sample.Timestamp); Number(row, ref offset, utc);
                Number(row, ref offset, sample.Interval); Number(row, ref offset, sample.Duration);
                Number(row, ref offset, sample.NativeUsed); Number(row, ref offset, sample.NativeHeap);
                Number(row, ref offset, sample.Gen0); Number(row, ref offset, sample.Gen1); Number(row, ref offset, sample.Gen2);
                Number(row, ref offset, sample.Dropped); Number(row, ref offset, sample.Errors);
                Number(row, ref offset, sample.ErrorFlags);
                row[offset - 1] = (byte)'\n';
                return offset;
            }

            private static void Number(Span<byte> output, ref int offset, long value)
            {
                if (!Utf8Formatter.TryFormat(value, output.Slice(offset), out int length))
                    throw new InvalidOperationException("Frame capture row buffer is too small.");
                offset += length;
                output[offset++] = (byte)',';
            }
        }
    }
}
