using BrikonYapi.Web.Data.Entities;

namespace BrikonYapi.Web.Models.ViewModels
{
    public class ProjectListViewModel
    {
        public List<Project> Projects { get; set; } = new();
        public string ActiveTab { get; set; } = "all";
        public int OngoingCount { get; set; }
        public int CompletedCount { get; set; }
    }
}
