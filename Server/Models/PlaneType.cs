using Server.DAL;

namespace Server.Models
{
    public class PlaneType
    {
        public int PlaneTypeID { get; set; }
        public string PlaneTypeName { get; set; }

        public PlaneType() { }

        // שליפת כל סוגי המטוסים
        public List<PlaneType> GetPlaneTypes()
        {
            DBservices dbs = new DBservices();
            return dbs.GetPlaneTypes();
        }

        // Inserts a new plane type into the database
        public int Insert()
        {
            DBservices dbs = new DBservices();
            return dbs.InsertPlaneType(this);
        }
    }
}
