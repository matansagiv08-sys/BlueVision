using Server.DAL;

namespace Server.Models
{
    public class ItemsInProduction
    {
        int serialNumber;
        int productItemID;
        int planeID;
        int priorityLevel;
        int workOrderID;
        int plannedQty;
        string comments;

        public int SerialNumber { get => serialNumber; set => serialNumber = value; }
        public int ProductItemID { get => productItemID; set => productItemID = value; }
        public int PlaneID { get => planeID; set => planeID = value; }
        public int PriorityLevel { get => priorityLevel; set => priorityLevel = value; }
        public int WorkOrderID { get => workOrderID; set => workOrderID = value; }
        public int PlannedQty { get => plannedQty; set => plannedQty = value; }
        public string Comments { get => comments; set => comments = value; }

        public ItemsInProduction() { }

        public ItemsInProduction(int serialNumber, int productItemID, int planeID, int priorityLevel, int workOrderID, int plannedQty, string comments)
        {
            SerialNumber = serialNumber;
            ProductItemID = productItemID;
            PlaneID = planeID;
            PriorityLevel = priorityLevel;
            WorkOrderID = workOrderID;
            PlannedQty = plannedQty;
            Comments = comments;
        }

        public int Insert()
        {
            DBservices dbs = new DBservices();
            return dbs.Insert(this);
        }

        public List<ItemsInProduction> Read()
        {
            DBservices dbs = new DBservices();
            return dbs.ReadItemsInProduction(); // נניח שזו המתודה שתצרי ב-DBservices
        }
    }
}
