using BrikonYapi.Web.Data.Entities;

namespace BrikonYapi.Web.Models.ViewModels
{
    public class HomeViewModel
    {
        public List<Project> HeroProjects { get; set; } = new();
        public List<Project> OngoingProjects { get; set; } = new();
        public List<Project> CompletedProjects { get; set; } = new();
        public List<Project> AllProjects { get; set; } = new();
        public Dictionary<string, string?> Settings { get; set; } = new();
        public List<Reference> References { get; set; } = new();
    }
}
