using System.ComponentModel;

namespace UrlSupervisor
{
    public class EditableMonitor : INotifyPropertyChanged
    {
        private string _name = "";
        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }

        private string _url = "";
        public string Url { get => _url; set { _url = value; OnPropertyChanged(nameof(Url)); } }

        private int _intervalSeconds = 10;
        public int IntervalSeconds { get => _intervalSeconds; set { _intervalSeconds = value; OnPropertyChanged(nameof(IntervalSeconds)); } }

        private int _timeoutSeconds = 5;
        public int TimeoutSeconds { get => _timeoutSeconds; set { _timeoutSeconds = value; OnPropertyChanged(nameof(TimeoutSeconds)); } }

        private int _order = 1;
        public int Order { get => _order; set { _order = value; OnPropertyChanged(nameof(Order)); } }

        private string _group = "";
        public string Group { get => _group; set { _group = value; OnPropertyChanged(nameof(Group)); } }

        private string _tags = "";
        public string Tags { get => _tags; set { _tags = value; OnPropertyChanged(nameof(Tags)); } }

        private bool _enabled = true;
        public bool Enabled { get => _enabled; set { _enabled = value; OnPropertyChanged(nameof(Enabled)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
