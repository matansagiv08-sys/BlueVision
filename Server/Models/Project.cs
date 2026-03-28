using Server.DAL;

namespace Server.Models
{
    public class Project
    {
        public int ProjectID { get; set; }
        public string ProjectName { get; set; }
        public DateTime DueDate { get; set; }
        public int PriorityLevel { get; set; }

        public Project() { }

        public List<Project> GetProjects() { DBservices dbs = new DBservices(); return dbs.GetProjects(); }
        public int Insert() { DBservices dbs = new DBservices(); return dbs.InsertProject(this); }
        public int Update() { DBservices dbs = new DBservices(); return dbs.UpdateProject(this); }
        public int Delete(int id) { DBservices dbs = new DBservices(); return dbs.DeleteProject(id); }
    }
}