using Server.DAL;

namespace Server.Models
{
    public class ItemInProduction
    {
        int serialNumber;
        string productionItemID;
        int planeID;
        int priorityLevel;
        int workOrderID;
        int plannedQty;
        string comments;

        public int SerialNumber { get => serialNumber; set => serialNumber = value; }
        public string ProductionItemID { get => productionItemID; set => productionItemID = value; }
        public int PlaneID { get => planeID; set => planeID = value; }
        public int PriorityLevel { get => priorityLevel; set => priorityLevel = value; }
        public int WorkOrderID { get => workOrderID; set => workOrderID = value; }
        public int PlannedQty { get => plannedQty; set => plannedQty = value; }
        public string Comments { get => comments; set => comments = value; }

        public ItemInProduction() { }

        // מתודת Read שמחזירה רשימה של כל הפריטים בייצור
        public List<ItemInProduction> Read()
        {
            DBservices dbs = new DBservices();
            return dbs.ReadItemsInProduction();
        }

        public int Insert()
        {
            DBservices dbs = new DBservices();
            return dbs.InsertItemInProduction(this);
        }

        public int Update()
        {
            DBservices dbs = new DBservices();
            return dbs.UpdateItemInProduction(this);
        }

        public int Delete()
        {
            DBservices dbs = new DBservices();
            return dbs.DeleteItemInProduction(this.SerialNumber, this.ProductionItemID);
        }
    }
}
