using Server.DAL;

namespace Server.Models
{
    public class ProductionItem
    {
        public string ProductionItemID { get; set; }
        public string ItemName { get; set; }

    //    public List<ProductionItem> GetProductionItems()
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.GetProductionItems();
    //    }

    }
}
