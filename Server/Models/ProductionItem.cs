using Server.DAL;

namespace Server.Models
{
    public class ProductionItem
    {
        public string ProductionItemID { get; set; }
        public string ItemName { get; set; }

        // שליפת כל סוגי הפריטים
    //    public List<ProductionItem> GetProductionItems()
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.GetProductionItems();
    //    }

    //    // הכנסת סוג פריט חדש 
    //    public int Insert()
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.InsertProductionItem(this);
    //    }

    //    // עדכון שם הפריט
    //    public int Update()
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.UpdateProductionItem(this);
    //    }

    //    // מחיקת פריט לפי ID
    //    public int Delete(int id)
    //    {
    //        DBservices dbs = new DBservices();
    //        return dbs.DeleteProductionItem(id);
    //    }
    }
}
