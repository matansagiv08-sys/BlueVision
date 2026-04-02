using Server.DAL;

namespace Server.Models
{
    public class Plane
    {
        public int PlaneID { get; set; }
        public PlaneType Type { get; set; }
        public int ProjectID { get; set; }
        public int PriorityLevel { get; set; }

        public List<ItemInProduction> Items { get; set; } = new List<ItemInProduction>();

        public double Progress
        {
            get
            {
                // אם אין חלקים למטוס הזה, האחוז הוא 0
                if (Items == null || Items.Count == 0) return 0;
                // סופרים כמה חלקים סיימו לגמרי 
                double fullyDoneItemsCount = Items.Count(i => i.IsFullyDone);
                // חישוב אחוז
                return (fullyDoneItemsCount / Items.Count) * 100;
            }
        }

        public Plane() { }

        //public List<Plane> GetPlanes() { DBservices dbs = new DBservices(); return dbs.GetPlanes(); }
        //public int Insert() { DBservices dbs = new DBservices(); return dbs.InsertPlane(this); }
        //public int Update() { DBservices dbs = new DBservices(); return dbs.UpdatePlane(this); }
        //public int Delete(int id) { DBservices dbs = new DBservices(); return dbs.DeletePlane(id); }
    }
}

