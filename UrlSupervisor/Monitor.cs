using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UrlSupervisor
{
    public class Monitor : INotifyPropertyChanged
    {
        public string Name { get; private set; }
        public string Url { get; private set; }
        public int Order { get; private set; }
        public int IntervalSeconds { get; private set; }
        public int TimeoutSeconds { get; private set; }
        public string Group { get; private set; } = "";
        public List<string> Tags { get; private set; } = new List<string>();

        public ObservableCollection<MonitorResult> LastResults { get; } = new();
        private const int MaxResults = 60;

        private bool _isRunning;
        public bool IsRunning { get => _isRunning; private set { _isRunning = value; OnPropertyChanged(nameof(IsRunning)); OnPropertyChanged(nameof(StatusText)); } }

        private bool _hasError;
        public bool HasError { get => _hasError; private set { _hasError = value; OnPropertyChanged(nameof(HasError)); } }

        private bool? _lastSuccess = null;
        private DateTime _lastErrorAt = DateTime.MinValue;
        private DateTime _startedAt = DateTime.UtcNow;
        private CancellationTokenSource? _cts;

        public string UptimeText { get; private set; } = "—";
        public string StatusText => IsRunning ? "En cours" : "Arrêté";

        public List<DowntimeEvent> Downtimes { get; } = new List<DowntimeEvent>();
        private DowntimeEvent? _openDowntime = null;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public event Action<Monitor, bool, bool>? StatusChanged;

        public Monitor(string name, string url, int order, int intervalSeconds, int timeoutSeconds, string group = "", IEnumerable<string>? tags = null)
        {
            Name = name;
            Url = url;
            Order = order;
            IntervalSeconds = Math.Max(1, intervalSeconds);
            TimeoutSeconds = Math.Max(1, timeoutSeconds);
            Group = group ?? "";
            if (tags != null) Tags = tags.ToList();
        }

        public async Task StartAsync()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            IsRunning = true;
            _startedAt = DateTime.UtcNow;
            await Task.Yield();
            _ = LoopAsync(_cts.Token);
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            IsRunning = false;
        }

        public async Task PingOnceAsync()
        {
            using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
            await DoCheckAsync(http, default);
        }

        public void UpdateFromEditable(EditableMonitor e)
        {
            Name = e.Name;
            Url = e.Url;
            Order = e.Order;
            IntervalSeconds = Math.Max(1, e.IntervalSeconds);
            TimeoutSeconds = Math.Max(1, e.TimeoutSeconds);
            Group = e.Group ?? "";
            Tags = (e.Tags ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Url));
            OnPropertyChanged(nameof(Order));
            OnPropertyChanged(nameof(IntervalSeconds));
            OnPropertyChanged(nameof(TimeoutSeconds));
            OnPropertyChanged(nameof(Group));
        }

        public void TickUptime()
        {
            DateTime since = _lastErrorAt == DateTime.MinValue ? _startedAt : _lastErrorAt;
            var span = DateTime.UtcNow - since;
            UptimeText = $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
            OnPropertyChanged(nameof(UptimeText));
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
            while (!ct.IsCancellationRequested)
            {
                await DoCheckAsync(http, ct);
                try { await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task DoCheckAsync(HttpClient http, CancellationToken ct)
        {
            bool success = false;
            try
            {
                var resp = await http.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, ct);
                success = ((int)resp.StatusCode) >= 200 && ((int)resp.StatusCode) < 400;
            }
            catch { success = false; }

            AppendResult(success);
            HasError = LastResults.Count > 0 && !LastResults[^1].Success;
            if (!success) _lastErrorAt = DateTime.UtcNow;

            if (_lastSuccess != null && _lastSuccess.Value != success)
            {
                StatusChanged?.Invoke(this, _lastSuccess.Value, success);
                if (!success)
                {
                    _openDowntime = new DowntimeEvent { Url = Url, Name = Name, StartUtc = DateTime.UtcNow };
                    Downtimes.Add(_openDowntime);
                }
                else
                {
                    if (_openDowntime != null && _openDowntime.EndUtc == null) _openDowntime.EndUtc = DateTime.UtcNow;
                    _openDowntime = null;
                }
            }
            if (_lastSuccess == null && !success)
            {
                _openDowntime = new DowntimeEvent { Url = Url, Name = Name, StartUtc = DateTime.UtcNow };
                Downtimes.Add(_openDowntime);
            }
            _lastSuccess = success;
        }

        private void AppendResult(bool success)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LastResults.Add(new MonitorResult
                {
                    Success = success,
                    Timestamp = DateTime.UtcNow,
                    Tooltip = DateTime.Now.ToString("HH:mm:ss") + " • " + (success ? "OK" : "KO")
                });
                while (LastResults.Count > MaxResults) LastResults.RemoveAt(0);
            });
        }
    }

    public class MonitorResult
    {
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
        public string Tooltip { get; set; } = "";
    }

    public class DowntimeEvent
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }
        public TimeSpan Duration => (EndUtc ?? DateTime.UtcNow) - StartUtc;
    }
}
