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

                // איסוף כל החלקים מכל המטוסים לרשימה אחת
                var allItems = Planes.SelectMany(p => p.Items).ToList();

                if (allItems.Count == 0) return 0;

                double totalDoneItems = allItems.Count(i => i.IsFullyDone);
                return (totalDoneItems / allItems.Count) * 100;
            }
        }

        public Project() { }

        public List<Project> GetProjects() { DBservices dbs = new DBservices(); return dbs.GetProjects(); }
        public int Insert() { DBservices dbs = new DBservices(); return dbs.InsertProject(this); }
        public int Update() { DBservices dbs = new DBservices(); return dbs.UpdateProject(this); }
        public int Delete(int id) { DBservices dbs = new DBservices(); return dbs.DeleteProject(id); }
    }
}