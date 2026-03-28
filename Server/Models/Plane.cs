using Server.DAL;

namespace Server.Models
{
    public class Plane
    {
        public int PlaneID { get; set; }
        public PlaneType Type { get; set; }
        public int ProjectID { get; set; }
        public int PriorityLevel { get; set; }

        public Plane() { }

        //public List<Plane> GetPlanes() { DBservices dbs = new DBservices(); return dbs.GetPlanes(); }
        //public int Insert() { DBservices dbs = new DBservices(); return dbs.InsertPlane(this); }
        //public int Update() { DBservices dbs = new DBservices(); return dbs.UpdatePlane(this); }
        //public int Delete(int id) { DBservices dbs = new DBservices(); return dbs.DeletePlane(id); }
    }
}

