using Server.DAL;

namespace Server.Models
{
    public class PlaneType
    {
        public int PlaneTypeID { get; set; }
        public string PlaneTypeName { get; set; }

        public PlaneType() { }

        public List<PlaneType> GetPlaneTypes()
        {
            DBservices dbs = new DBservices();
            return dbs.GetPlaneTypes();
        }

        public int Insert()
        {
            DBservices dbs = new DBservices();
            return dbs.InsertPlaneType(this);
        }

        public int Delete(int id)
        {
            DBservices dbs = new DBservices();
            return dbs.DeletePlaneType(id);
        }
    }
}
