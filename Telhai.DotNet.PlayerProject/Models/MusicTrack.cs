using System.Collections.Generic;

namespace Telhai.DotNet.PlayerProject.Models
{
    public class MusicTrack
    {
        public string Title { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? ArtworkUrl { get; set; }
        public List<string> LocalImages { get; set; } = new List<string>();
    }
}