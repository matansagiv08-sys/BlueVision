using Server.DAL;

namespace Server.Models
{
    public class PlaneType
    {
        public int PlaneTypeID { get; set; }
        public string PlaneTypeName { get; set; }

        public PlaneType() { }

        // Retrieves all plane types from the database
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

        // Deletes a plane type from the database by ID
        public int Delete(int id)
        {
            DBservices dbs = new DBservices();
            return dbs.DeletePlaneType(id);
        }
    }
}
