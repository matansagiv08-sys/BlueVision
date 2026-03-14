using Server.DAL;

namespace Server.Models
{
    public class Plane
    {
        public int PlaneID { get; set; }
        public int PlaneTypeID { get; set; }
        public int ProjectID { get; set; }
        public byte PriorityLevel { get; set; }

        public Plane() { }

        // מתודות עזר ל-DB
        public List<Plane> GetPlanes() { DBservices dbs = new DBservices(); return dbs.GetPlanes(); }
        public int Insert() { DBservices dbs = new DBservices(); return dbs.InsertPlane(this); }
        public int Update() { DBservices dbs = new DBservices(); return dbs.UpdatePlane(this); }
        public int Delete(int id) { DBservices dbs = new DBservices(); return dbs.DeletePlane(id); }
    }
}

