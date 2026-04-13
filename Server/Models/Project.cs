using Server.DAL;

namespace Server.Models
{
    public class Project
    {
        public int ProjectID { get; set; }
        public string ProjectName { get; set; }
        public DateTime DueDate { get; set; }
        public int PriorityLevel { get; set; }

        public List<Plane> Planes { get; set; } = new List<Plane>();

        public double Progress
        {
            get
            {
                if (Planes == null || Planes.Count == 0) return 0;
                return Planes.Average(p => p.Progress);
            }
        }

        public Project() { }


        // Calls DBservices to retrieve the list of projects from the database and returns it
        public List<Project> GetProjects() { DBservices dbs = new DBservices(); return dbs.GetProjects(); }

        public List<Project> GetFullProjectsStatus()
        {
            DBservices dbs = new DBservices();
            return dbs.GetFullProjectsStatus();
        }
    }
}
