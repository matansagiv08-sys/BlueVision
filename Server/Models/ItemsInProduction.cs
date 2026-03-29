using Server.DAL;

namespace Server.Models
{
    public class ItemInProduction
    {
        public int SerialNumber { get; set; }
        public ProductionItem ProductionItem { get; set; } 
        public Plane PlaneID { get; set; }
        public int PriorityLevel { get; set; }
        public int WorkOrderID { get; set; }
        public int PlannedQty { get; set; }
        public string Comments { get; set; }
        public List<ProductionItemStage> Stages { get; set; } = new List<ProductionItemStage>();

        public ItemInProduction() { }

        //  שמחזירה רשימה של כל הפריטים בייצור
        public List<ItemInProduction> Read()
        {
            DBservices dbs = new DBservices();
            return dbs.ReadItemsInProduction();
        }

    //    public int Insert()
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.InsertItemInProduction(this);
    //    }

    //    public int Update()
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.UpdateItemInProduction(this);
    //    }

    //    public int Delete()
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.DeleteItemInProduction(this.SerialNumber, this.ProductionItemID);
    //    }
    }
}
