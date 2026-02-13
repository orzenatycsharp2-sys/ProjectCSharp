using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using Telhai.DotNet.PlayerProject.Models;

namespace Telhai.DotNet.PlayerProject
{
    public class EditSongViewModel : INotifyPropertyChanged
    {
        private MusicTrack _track;
        public MusicTrack Track => _track;

        public string Title
        {
            get => _track.Title;
            set { _track.Title = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> Images { get; set; }

        public ICommand AddImageCommand { get; }
        public ICommand RemoveImageCommand { get; }
        public ICommand SaveCommand { get; }

        public EditSongViewModel(MusicTrack track)
        {
            _track = track;
            Images = new ObservableCollection<string>(track.LocalImages);

            AddImageCommand = new RelayCommand(_ => {
                var dlg = new OpenFileDialog { Filter = "Images|*.jpg;*.png;*.jpeg" };
                if (dlg.ShowDialog() == true)
                {
                    Images.Add(dlg.FileName);
                    _track.LocalImages.Add(dlg.FileName);
                }
            });

            RemoveImageCommand = new RelayCommand(img => {
                if (img is string path)
                {
                    Images.Remove(path);
                    _track.LocalImages.Remove(path);
                }
            });

            SaveCommand = new RelayCommand(win => (win as System.Windows.Window)?.Close());
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}